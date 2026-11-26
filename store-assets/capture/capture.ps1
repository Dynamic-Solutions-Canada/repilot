# Repilot's Store screenshots in one listing language (store-assets/README.md, "Screenshots"). ASCII only.
#
# Run by capture.sh through app_test_ui, in the guest's desktop session, after screenshot-desktop.ps1
# -Mode Prepare, with a dev package installed and Windows in this locale's display language. Reads its
# inputs from C:\apptest\capture-input.json (locale, scale) and this locale's typed text and app names
# from sample-data.json. For each shot it writes synthetic settings (dark theme, the app's language,
# the action), opens the settings window centred on the work area and saves the whole desktop with
# its .json sidecar to C:\apptest\out.
#
# Controls are found by automation ID (Find-ById), never by translated text, except the installed app
# chosen in the App or link shot, whose name is Windows' own and comes from sample-data.json. Sizes
# are given at 100% and multiplied by the scale, because the helpers work in physical pixels.
$ErrorActionPreference = 'Stop'
. C:\apptest\ui-lib.ps1

$inputs = Get-Content C:\apptest\capture-input.json -Raw | ConvertFrom-Json
$locale = $inputs.locale
$scale = [int]$inputs.scale / 100
$sample = (Get-Content C:\apptest\sample-data.json -Raw -Encoding UTF8 | ConvertFrom-Json).locales.$locale
if (-not $sample) { throw "sample-data.json has no entry for $locale" }

<# A length at 100% display scale, in physical pixels at the capture's scale. #>
function Px([int]$Logical) { [int]($Logical * $scale) }

<#
A plain click on blank page space clears the keyboard focus rectangles that automation leaves: the
right end of the page title row is empty on every page. Then park the pointer on empty desktop,
away from the window and the taskbar, so no hover shows.
#>
function Clear-Focus($Window) {
    $frame = Get-WindowFrame $Window
    [UiNative]::SetCursorPos($frame.X + $frame.Width - (Px 80), $frame.Y + (Px 85)) | Out-Null
    Start-Sleep -Milliseconds 200
    [UiNative]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero)
    [UiNative]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
    Start-Sleep -Milliseconds 500
    [UiNative]::SetCursorPos((Px 1900), (Px 20)) | Out-Null
    Start-Sleep -Seconds 1
}

<#
Write Repilot's settings for one shot: dark theme, the app's language set to this locale, and the
Copilot key's action ($ActionJson, Repilot's own action shape). Strings go through ConvertTo-Json, so
any language's text is escaped correctly, and the file is UTF-8 without a byte order mark.
#>
function Write-Settings([string]$DataDir, [string]$ActionJson) {
    $settings = [ordered]@{
        AppTheme = 2
        LanguageOverride = $locale
        Action = ($ActionJson | ConvertFrom-Json)
        SettingsWindow = [ordered]@{ X = 40; Y = 20; Width = 1040; Height = 700 }
    }
    [IO.File]::WriteAllText((Join-Path $DataDir 'settings.json'), ($settings | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding $false))
}

<# Search the installed-apps list for this language's app and select it, as a user would. #>
function Select-InstalledApp($Window) {
    # The list fills in the background, slowly on a fresh guest, so wait for it before searching.
    $listItem = New-Object System.Windows.Automation.PropertyCondition($UiA::ControlTypeProperty, [System.Windows.Automation.ControlType]::ListItem)
    $list = Find-ById $Window 'AppList'
    for ($i = 0; $i -lt 30 -and -not $list.FindFirst('Descendants', $listItem); $i++) { Start-Sleep -Seconds 1 }

    # The AutoSuggestBox's text field is the Edit inside it.
    $edit = New-Object System.Windows.Automation.PropertyCondition($UiA::ControlTypeProperty, [System.Windows.Automation.ControlType]::Edit)
    $search = (Find-ById $Window 'AppSearch').FindFirst('Descendants', $edit)
    $search.GetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern).SetValue($sample.appSearch)

    # The list item's own name is not the app's name, so find the item's text and click it.
    $app = $null
    for ($i = 0; $i -lt 15 -and -not $app; $i++) {
        Start-Sleep -Seconds 1
        $app = Find-ByName $list $sample.appName 'Text'
    }
    if (-not $app) { throw "$($sample.appName) is not in the installed-apps list after searching '$($sample.appSearch)'" }
    Invoke-Click $app
    Start-Sleep -Seconds 2
}

# --- Check the guest, then start clean --------------------------------------------------------

# Windows itself must already be in this language; the app is set to it in each shot's settings.
$ui = (Get-UICulture).Name
if ($ui -ne $locale) { throw "Windows is in $ui, not ${locale}; switch it first (capture.sh, CAPTURE_PREPARE_LOCALE)" }

New-Item -ItemType Directory -Force $UiOut | Out-Null
Get-ChildItem $UiOut -File | Where-Object Extension -in '.png', '.json' | Remove-Item
Get-Process Repilot -ErrorAction SilentlyContinue | Stop-Process -Force

$pkg = Get-AppxPackage TechnicallyReal.Repilot
$dataDir = Join-Path $env:LOCALAPPDATA "Packages\$($pkg.PackageFamilyName)\LocalCache\Roaming\Repilot"
New-Item $dataDir -ItemType Directory -Force | Out-Null

# Show the key as assigned, the way Windows Settings records it; restored at the end.
$keyPath = 'HKCU:\Software\Microsoft\Windows\Shell\BrandedKey'
$hadKey = Test-Path $keyPath
New-Item $keyPath -Force | Out-Null
Set-ItemProperty $keyPath BrandedKeyChoiceType 'App'
Set-ItemProperty $keyPath AppAumid "$($pkg.PackageFamilyName)!App"

# --- The shots ---------------------------------------------------------------------------------

# The Text action types this language's phrase, and the App or link action opens Notepad under its
# name in this language (the shot also picks it in the list). Each Action is Repilot's own JSON shape.
$textAction = [ordered]@{ Type = 4; Text = $sample.text } | ConvertTo-Json -Compress
$appAction = [ordered]@{ Type = 2; LaunchPath = 'notepad.exe'; LaunchDisplayName = $sample.appName } | ConvertTo-Json -Compress
$shots = @(
    @{ Name = '00-home-dark';             Page = 'NavHome';   Action = '{ "Type": 3, "WindowsFunctionId": "task-view" }' },
    @{ Name = '01-shortcut-dark';         Page = 'NavAction'; Action = '{ "Type": 1, "Combo": { "Modifiers": 2, "VirtualKey": 9 } }' },
    @{ Name = '02-text-dark';             Page = 'NavAction'; Action = $textAction },
    @{ Name = '03-app-or-link-dark';      Page = 'NavAction'; Action = $appAction },
    @{ Name = '04-windows-function-dark'; Page = 'NavAction'; Action = '{ "Type": 3, "WindowsFunctionId": "task-view" }' }
)

foreach ($shot in $shots) {
    Write-Settings $dataDir $shot.Action
    Start-Process "shell:AppsFolder\$($pkg.PackageFamilyName)!Settings"

    # The first launch after an install can take a while; wait up to 60 s for the window.
    $window = $null
    for ($i = 0; $i -lt 30 -and -not $window; $i++) {
        Start-Sleep -Seconds 2
        $window = Get-AppWindows Repilot | Select-Object -First 1
    }
    if (-not $window) { throw "No Repilot window for $($shot.Name)" }
    Start-Sleep -Seconds 3

    # The same frame size as the earlier captures, centred on the work area.
    Set-WindowCentered $window (Px 1026) (Px 693)
    Start-Sleep -Seconds 1

    if ($shot.Page -ne 'NavHome') {
        $item = Find-ById $window $shot.Page
        if (-not $item) { throw "No navigation item $($shot.Page)" }
        try { $item.GetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern).Select() } catch { Invoke-Click $item }
        Start-Sleep -Seconds 4
    }
    if ($shot.Name -eq '03-app-or-link-dark') {
        Select-InstalledApp $window
    }

    Clear-Focus $window
    # Focuses the window, captures the whole screen and writes <name>.png with its .json sidecar.
    Save-DesktopCapture $shot.Name $window

    Get-Process Repilot -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Start-Sleep -Seconds 4
}

if (-not $hadKey) { Remove-Item $keyPath -Recurse -Force }
Write-Output "capture finished: $locale"
