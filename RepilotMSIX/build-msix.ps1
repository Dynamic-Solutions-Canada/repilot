<#
.SYNOPSIS
    Builds the Repilot MSIX package (sideload or Store upload).

.DESCRIPTION
    1. Reads the version from Directory.Build.props (single source of truth)
    2. Publishes the app self-contained (bundles the .NET runtime), WindowsPackageType=MSIX
    3. Assembles the MSIX layout (app files + compiled XAML + stamped manifest + Images + Public)
    4. Generates resources.pri via makepri
    5. Packages with makeappx.exe
    6. Signs with signtool.exe using the PFX passed in -TrustedPfxPath

    Signing material is only ever a parameter (SEC-11, DIST-2): there is no generated certificate
    and no written password. The build tooling passes the shared dev certificate. Use -NoSign for
    Microsoft Store uploads (the Store re-signs during ingestion).

.EXAMPLE
    .\build-msix.ps1 -NoSign
    .\build-msix.ps1 -Platform x64 -TrustedPfxPath C:\path\signing.pfx -TrustedPfxPassword <password>
#>
param(
    [ValidateSet("x64", "ARM64")]
    [string]$Platform = $(if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "ARM64" } else { "x64" }),

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [string]$TrustedPfxPath = "",
    [string]$TrustedPfxPassword = "",
    [switch]$NoSign
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

# ── Paths ──────────────────────────────────────────────────────────────────────
$msixDir     = $PSScriptRoot
$repoRoot    = Split-Path $msixDir -Parent
$mainProj    = Join-Path $repoRoot "Repilot\Repilot.csproj"
$keyProj     = Join-Path $repoRoot "RepilotKey\RepilotKey.csproj"
$manifestSrc = Join-Path $msixDir  "Package.appxmanifest"
$imagesDir   = Join-Path $msixDir  "Images"
$publicDir   = Join-Path $msixDir  "Public"

$rid        = if ($Platform -eq "ARM64") { "win-arm64" } else { "win-x64" }
$publishDir = Join-Path $repoRoot "Repilot\bin\$Platform\$Configuration\net10.0-windows10.0.22000.0\$rid\publish"
$keyPublishDir = Join-Path $repoRoot "RepilotKey\bin\$Platform\$Configuration\net10.0-windows10.0.22000.0\$rid\publish"
$layoutDir  = Join-Path $msixDir  "bin\msix-layout\$Platform"
$outputDir  = Join-Path $msixDir  "bin\msix-output"

# Packaging tools (makeappx/makepri/signtool) for the native host arch. Prefer an
# installed Windows SDK; otherwise use the Microsoft.Windows.SDK.BuildTools NuGet
# package (acquiring it if it isn't cached).
$sdkHostArch = if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { "arm64" } else { "x64" }
$buildToolsVersion = "10.0.26100.4654"

function Find-SdkBin([string]$hostArch) {
    # 1) Installed Windows SDK
    $kitsRoot = "C:\Program Files (x86)\Windows Kits\10\bin"
    if (Test-Path $kitsRoot) {
        $hit = Get-ChildItem $kitsRoot -Directory |
            Where-Object { $_.Name -match '^10\.' } |
            Sort-Object { [version]$_.Name } -Descending |
            ForEach-Object { Join-Path $_.FullName $hostArch } |
            Where-Object { Test-Path (Join-Path $_ "makeappx.exe") } |
            Select-Object -First 1
        if ($hit) { return $hit }
    }
    # 2) Microsoft.Windows.SDK.BuildTools NuGet package (global cache)
    $nugetRoot = if ($env:NUGET_PACKAGES) { $env:NUGET_PACKAGES } else { Join-Path $env:USERPROFILE ".nuget\packages" }
    $btRoot = Join-Path $nugetRoot "microsoft.windows.sdk.buildtools"
    if (Test-Path $btRoot) {
        foreach ($pkg in (Get-ChildItem $btRoot -Directory | Sort-Object { [version]$_.Name } -Descending)) {
            $binRoot = Join-Path $pkg.FullName "bin"
            if (-not (Test-Path $binRoot)) { continue }
            $hit = Get-ChildItem $binRoot -Directory |
                Sort-Object { [version]$_.Name } -Descending |
                ForEach-Object { Join-Path $_.FullName $hostArch } |
                Where-Object { Test-Path (Join-Path $_ "makeappx.exe") } |
                Select-Object -First 1
            if ($hit) { return $hit }
        }
    }
    return $null
}

$sdkBin = Find-SdkBin $sdkHostArch
if (-not $sdkBin) {
    Write-Host "Packaging tools not found - acquiring Microsoft.Windows.SDK.BuildTools via NuGet..." -ForegroundColor Yellow
    $tmp = Join-Path $env:TEMP "crk-sdktools"
    New-Item $tmp -ItemType Directory -Force | Out-Null
    @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
  <ItemGroup><PackageReference Include="Microsoft.Windows.SDK.BuildTools" Version="$buildToolsVersion" /></ItemGroup>
</Project>
"@ | Set-Content (Join-Path $tmp "tools.csproj")
    dotnet restore (Join-Path $tmp "tools.csproj") | Out-Null
    $sdkBin = Find-SdkBin $sdkHostArch
}
if (-not $sdkBin) { Write-Error "Could not find or acquire packaging tools (makeappx/makepri/signtool)."; exit 1 }
Write-Host "Using packaging tools: $sdkBin" -ForegroundColor DarkCyan
$makeappx = Join-Path $sdkBin "makeappx.exe"
$signtool = Join-Path $sdkBin "signtool.exe"
$makepri  = Join-Path $sdkBin "makepri.exe"

# ── Version ─────────────────────────────────────────────────────────────────────
$propsXml = [xml](Get-Content (Join-Path $repoRoot "Directory.Build.props"))
$version  = $propsXml.SelectSingleNode("//Version").InnerText
if (-not $version) { Write-Error "Cannot read <Version> from Directory.Build.props"; exit 1 }
$msixVersion = if ($version -match '^\d+\.\d+\.\d+$') { "$version.0" } else { $version }
Write-Host "Version: $msixVersion" -ForegroundColor Cyan

# Named with the version, and set here rather than in the paths block above because the version is
# not known until it has been read. Every build used to write the same two filenames, so the folder
# held whichever build ran last with nothing on disk to say which - and the file you upload to the
# Store is chosen by eye. Partner Center reads the version from the manifest either way; this is so
# the human picking the file cannot get it wrong.
$msixFile = Join-Path $outputDir "Repilot-$version-$Platform.msix"

# -- Signing certificate (checked before publishing) ---------------------------------
# Signing material is only ever a parameter (FIX-060): no fallback certificate is generated.
$signCert = $null
if ($NoSign) {
    Write-Host "Skipping signing (Store upload mode)" -ForegroundColor Yellow
} else {
    if ([string]::IsNullOrEmpty($TrustedPfxPath) -or -not (Test-Path $TrustedPfxPath)) {
        Write-Error "No signing certificate. Pass -TrustedPfxPath and -TrustedPfxPassword (the build tooling does), or use -NoSign for an unsigned Store upload."
        exit 1
    }
    $signCert = New-Object System.Security.Cryptography.X509Certificates.X509Certificate2($TrustedPfxPath, $TrustedPfxPassword)
}

# ── Step 1: Publish (self-contained) ────────────────────────────────────────────
Write-Host "`n=== Publishing Repilot ($Platform $Configuration) ===" -ForegroundColor Cyan
dotnet publish $mainProj -c $Configuration -r $rid -p:Platform=$Platform `
    --self-contained -p:PublishSingleFile=false -p:WindowsPackageType=MSIX
if ($LASTEXITCODE -ne 0) { Write-Error "dotnet publish failed"; exit 1 }

# Key handler: self-contained, ReadyToRun, single-file, trimmed (settings from csproj).
Write-Host "`n=== Publishing RepilotKey (handler) ===" -ForegroundColor Cyan
dotnet publish $keyProj -c $Configuration -r $rid -p:Platform=$Platform
if ($LASTEXITCODE -ne 0) { Write-Error "key handler publish failed"; exit 1 }

# ── Step 2: Assemble layout ──────────────────────────────────────────────────────
Write-Host "`n=== Assembling MSIX layout ===" -ForegroundColor Cyan
if (Test-Path $layoutDir) { Remove-Item $layoutDir -Recurse -Force }
New-Item $layoutDir -ItemType Directory -Force | Out-Null

Copy-Item "$publishDir\*" $layoutDir -Recurse -Force

# Drop in the single-file key handler exe.
Copy-Item (Join-Path $keyPublishDir "RepilotKey.exe") $layoutDir -Force

# dotnet publish omits compiled XAML (.xbf) — copy from the RID build dir.
$ridBuildDir = Split-Path $publishDir -Parent
Get-ChildItem $ridBuildDir -Filter "*.xbf" -Recurse |
    Where-Object { $_.FullName -notlike "*\publish\*" } |
    ForEach-Object {
        $rel = $_.FullName.Substring($ridBuildDir.Length + 1)
        $dest = Join-Path $layoutDir $rel
        $destDir = Split-Path $dest -Parent
        if (-not (Test-Path $destDir)) { New-Item $destDir -ItemType Directory -Force | Out-Null }
        Copy-Item $_.FullName $dest -Force
    }

# Stamp manifest placeholders
$msixArch = if ($Platform -eq "ARM64") { "arm64" } else { "x64" }
$manifestContent = (Get-Content $manifestSrc -Raw) `
    -replace 'ARCH_PLACEHOLDER', $msixArch `
    -replace 'VERSION_PLACEHOLDER', $msixVersion

# For signed (non-Store) builds, swap Publisher to the signing certificate's subject.
if ($signCert) {
    if ($manifestContent -match '<Identity[^>]+Publisher="([^"]+)"') {
        $manifestContent = $manifestContent.Replace("Publisher=`"$($Matches[1])`"", "Publisher=`"$($signCert.Subject)`"")
        Write-Host "  Stamped Publisher=$($signCert.Subject)"
    }
}

Set-Content -Path (Join-Path $layoutDir "AppxManifest.xml") -Value $manifestContent -NoNewline

# Images + Public folder (PublicFolder declared by the provider extension)
Copy-Item $imagesDir (Join-Path $layoutDir "Images") -Recurse -Force
Copy-Item $publicDir (Join-Path $layoutDir "Public") -Recurse -Force
Write-Host "  Layout ready: $layoutDir"

# ── Step 3: resources.pri ─────────────────────────────────────────────────────────
Write-Host "`n=== Generating resources.pri ===" -ForegroundColor Cyan
$existingPri = Join-Path $layoutDir "resources.pri"
# Remove the publish output's index, including any language-split files.
Get-ChildItem $layoutDir -Filter "resources*.pri" | Remove-Item -Force

# Stage the settings app's strings (Repilot\Strings\<lang>\Resources.resw) in the layout
# so the RESW indexer finds them. en-CA and fr-CA are copies of en-US and fr-FR: the
# Canadian wording is shared today, but every language the manifest declares gets its
# own candidates. The staged .resw files are removed again before packing.
$stringsSrc   = Join-Path $repoRoot "Repilot\Strings"
$stringsStage = Join-Path $layoutDir "Strings"
Copy-Item $stringsSrc $stringsStage -Recurse -Force
foreach ($alias in @(@("en-CA", "en-US"), @("fr-CA", "fr-FR"))) {
    Copy-Item (Join-Path $stringsSrc $alias[1]) (Join-Path $stringsStage $alias[0]) -Recurse -Force
}

$priconfigFile = Join-Path $layoutDir "priconfig.xml"
& $makepri createconfig /cf $priconfigFile /dq en-US /o
if ($LASTEXITCODE -ne 0) { Write-Error "makepri createconfig failed"; exit 1 }

# Language splitting writes resources.language-*.pri files that only a bundle's resource
# packs load. This flat MSIX would silently lose every translation, so remove the
# <packaging> section and keep all languages in the one resources.pri.
[xml]$priConfig = Get-Content $priconfigFile -Raw
$packagingNode = $priConfig.resources.SelectSingleNode("packaging")
if ($packagingNode) { [void]$priConfig.resources.RemoveChild($packagingNode) }
$priConfig.Save($priconfigFile)

& $makepri new /pr $layoutDir /cf $priconfigFile /mn (Join-Path $layoutDir "AppxManifest.xml") /of $existingPri /o
if ($LASTEXITCODE -ne 0) { Write-Error "makepri new failed"; exit 1 }
Remove-Item $priconfigFile -Force -ErrorAction SilentlyContinue
Remove-Item $stringsStage -Recurse -Force

# Verify the index before packing: no split files, and a known string resolves in every
# declared language. This is the check that fails loudly instead of shipping English only.
if (Get-ChildItem $layoutDir -Filter "resources.*.pri") {
    Write-Error "makepri produced language-split PRI files; translations would be lost"; exit 1
}
$priDump = Join-Path $msixDir "bin\resources-$Platform.pri.xml"
& $makepri dump /if $existingPri /of $priDump /dt detailed /o
if ($LASTEXITCODE -ne 0) { Write-Error "makepri dump failed"; exit 1 }
[xml]$dump = Get-Content $priDump -Raw
# Dump shape: NamedResource(uri) > Candidate > QualifierSet > Qualifier(name="Language", value).
$probeXPath = "//NamedResource[contains(@uri, '/Resources/NavHome/Content')]/Candidate/QualifierSet/Qualifier[@name='Language']/@value"
$probeLanguages = @($dump.SelectNodes($probeXPath) | ForEach-Object { $_.Value })
if ($probeLanguages.Count -eq 0) {
    Write-Error "resources.pri has no settings-app strings (NavHome.Content missing)"; exit 1
}
[xml]$stampedManifest = Get-Content (Join-Path $layoutDir "AppxManifest.xml") -Raw
foreach ($resource in $stampedManifest.Package.Resources.Resource) {
    if (-not ($probeLanguages | Where-Object { $_ -eq $resource.Language })) {
        Write-Error "resources.pri has no $($resource.Language) strings (looked for NavHome.Content)"; exit 1
    }
}
Write-Host "  Strings indexed for: $(($stampedManifest.Package.Resources.Resource | ForEach-Object { $_.Language }) -join ', ')"

# ── Step 4: Package ────────────────────────────────────────────────────────────────
Write-Host "`n=== Packaging MSIX ===" -ForegroundColor Cyan
if (-not (Test-Path $outputDir)) { New-Item $outputDir -ItemType Directory -Force | Out-Null }
if (Test-Path $msixFile) { Remove-Item $msixFile -Force }
& $makeappx pack /d $layoutDir /p $msixFile /o
if ($LASTEXITCODE -ne 0) { Write-Error "makeappx pack failed"; exit 1 }

# ── Step 5: Sign ───────────────────────────────────────────────────────────────────
if ($NoSign) {
    Write-Host "`n=== Skipping MSIX signing (Store upload) ===" -ForegroundColor Yellow
} else {
    Write-Host "`n=== Signing MSIX ===" -ForegroundColor Cyan
    & $signtool sign /fd SHA256 /f $TrustedPfxPath /p $TrustedPfxPassword /tr http://timestamp.digicert.com /td SHA256 $msixFile
    if ($LASTEXITCODE -ne 0) { Write-Error "signtool sign failed"; exit 1 }
}

$size = [math]::Round((Get-Item $msixFile).Length / 1MB, 1)
Write-Host "`n=== SUCCESS ===" -ForegroundColor Green
Write-Host "  MSIX:     $msixFile ($size MB)"
Write-Host "  Version:  $msixVersion  Platform: $Platform"
Write-Host "  Install:  Add-AppxPackage -Path '$msixFile' -ForceUpdateFromAnyVersion"
Write-Host "  Then: Settings > Bluetooth & devices > Keyboard > Customize Copilot key > Custom > Repilot"
