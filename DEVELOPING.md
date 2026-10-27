# Developing Repilot

Repilot remaps the Windows 11 Copilot key via the official Microsoft "Copilot
hardware key provider" model. It ships as a signed **MSIX** (required for it to be
assignable in Settings).

## Working with Codex

Open this repository in Codex. Project instructions live in [AGENTS.md](AGENTS.md),
which Codex loads automatically. Keep architecture constraints, build guidance, and
project-specific gotchas there so future tasks use the same guidance. See the
[official instruction-file documentation](https://developers.openai.com/codex/guides/agents-md)
for how Codex discovers and combines instructions.

## Architecture (two processes, by design)

The keypress path is deliberately **thin and non-resident** — nothing runs in the
background just to wait for the key:

- **`RepilotKey.exe`** (`RepilotKey/`) — a tiny self-contained (ReadyToRun, trimmed,
  single-file) exe. It is the registered Copilot-key provider (Start tile
  **"Repilot Key"**). On a key press Windows launches it; it reads `settings.json`,
  performs the action (synthesized keystroke via `SendInput`, or `Process.Start` for
  an app/URI), and **exits**. No window, no resident process, ~ms startup. (One csproj
  flag from full Native AOT if the VC++ build tools are present.)
- **`Repilot.exe`** (`Repilot/`) — the WinUI 3 settings UI (Start tile **"Repilot"**).
  Launched on demand to configure the action and check for updates. Never resident,
  never in the keypress path.

Both ship in one MSIX (`RepilotMSIX/`) with two `<Application>` entries:

- `Id="App"` is the handler — named "App" so the user's key-assignment AUMID (`…!App`)
  survives updates.
- `Id="Settings"` is the WinUI app.

> **The provider (`Id="App"`) must be listed in the app list — do not set
> `AppListEntry="none"` on it.** The "Customize Copilot key" picker only offers
> visible, launchable apps; a hidden provider still registers in the AppExtension
> catalog but Settings rejects it as "no app meets the hardware key criteria." That is
> why both apps have a Start tile.

The handler and the settings app share their model/catalog/executor code via **linked
source files**, so those files stay WinUI-free and AOT/trim-safe (no NLog, no
reflection-heavy code).

## Building

Run the build commands from the repository or its project/package directories so `global.json` selects the SDK. It requires stable .NET SDK 10.0.403 or a later 10.0 feature band/patch. Both packaging workflows install the version from that file. The SDK continues to choose runtime packs for each architecture; `Directory.Build.targets` rejects resolved .NET 10 runtime packs older than 10.0.14, including explicit overrides.

This is the minimum build baseline for [CVE-2026-111497](https://github.com/dotnet/runtime/security/advisories/GHSA-p3jp-9v83-76w9). It does not establish that Repilot processes attacker-controlled Brotli input. Both executables in the MSIX are self-contained, so an existing package needs a fresh restore, rebuild and Store update to receive the runtime fix. Updating the machine's shared runtime alone does not update either packaged executable.

**Settings app (dev):**
```powershell
dotnet build Repilot/Repilot.csproj -c Debug
```

**MSIX package (assignable / Store):**
```powershell
# Needs the Windows SDK packaging tools (makeappx/makepri/signtool). The build
# script finds them in an installed SDK or the Microsoft.Windows.SDK.BuildTools
# NuGet package (acquiring it if needed).
powershell -File RepilotMSIX/generate-msix-images.ps1   # regenerate the icon/tile art + the .ico
powershell -File RepilotMSIX/build-msix.ps1             # sideload (dev-signed)
powershell -File RepilotMSIX/build-msix.ps1 -NoSign     # Store upload
```

The script publishes both exes (WinUI self-contained + the handler) into the package.
Before Store submission, set `<Identity>` `Name`/`Publisher` in
`RepilotMSIX/Package.appxmanifest` to your Partner Center values. Bump `<Version>` in
`Directory.Build.props` for each update (MSIX blocks same-version re-installs).

## Requirements

- Windows 11 (build 22621+)
- Stable .NET 10 SDK 10.0.403 or newer, selected by `global.json`
- Windows SDK packaging tools to build the MSIX
- Full Native AOT for the handler additionally needs the VC++ build tools (optional)

## Publishing to the Microsoft Store

The `store-publish.yml` workflow submits to product `9PB5FJ08PNVJ`.

The workflow pins [Microsoft Store CLI v0.4.3](https://github.com/microsoft/msstore-cli/releases/tag/v0.4.3),
builds unsigned x64 and ARM64 packages on one runner, combines them into a real
architecture-aware `.msixbundle` stamped with the app version, and wraps that in a versioned `.msixupload`,
and submits directly to Partner Center on `v*` tags. It uploads no public binary artifacts.
MakeAppx must receive `/bv` so the bundle does not get a date-based version that
could outrank the next app release.
Manual dispatch defaults `no_commit` to true for draft review; disable it to commit the submission.
Certification and the submission's publishing settings determine when it becomes available.

The published base price is US $0.99. The API may report it as `PriceId: "Base"`, which
the CLI cannot round-trip. This workflow explicitly supplies `Tier1012`, the US $0.99
tier identified by a [Microsoft maintainer](https://github.com/microsoft/msstore-cli/pull/175#issuecomment-5791491206).
Tier pricing can change converted prices in other markets; review the ingested submission
in Partner Center. The workflow checks for a pending submission before invoking the CLI,
because the CLI would otherwise delete an existing draft.

The repository secrets are `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`,
`AZURE_AD_APPLICATION_SECRET`, and `SELLER_ID`. All four were present when checked on
September 22, 2026. The first tier-based submission accepted authentication and Tier1012,
but a ZIP of loose MSIX packages ingested as x64 only. Submission 7 was withdrawn to
draft before publication. The API reports it as `Canceled`, so it cannot be edited.
Run `replace_canceled_draft` with its exact ID and `no_commit` disabled to remove only
that canceled submission and publish a corrected bundle. Verify both architectures
and pricing in Partner Center after ingestion.
Future tag runs use the MSIX bundle format.
The corrected [Store run](https://github.com/RyanEwen/Repilot/actions/runs/35898180528)
committed Submission 7 (`1152921505701962545`) with a 1.0.19.0 bundle covering
x64 and ARM64. Partner Center shows US $0.99, with 36 of 240 regional prices changed
from the published Base schedule. It is in certification and will publish automatically
after passing.

**Releasing:** bump `<Version>` in `Directory.Build.props`, commit, create the matching
`vX.Y.Z` tag and push. Verify both the notes-only release and Store submission workflows.

For manual fallback, generate images and build both packages:

```powershell
.\RepilotMSIX\generate-msix-images.ps1
.\RepilotMSIX\build-msix.ps1 -Platform x64 -NoSign
.\RepilotMSIX\build-msix.ps1 -Platform ARM64 -NoSign
```

Upload the individual versioned `.msix` files from `RepilotMSIX/bin/msix-output` in
Partner Center. The `.msixupload` container is used by CI's CLI submission.

### What a GitHub release contains

The tag runs [`Build and Package`](.github/workflows/build-msix.yml), which publishes a
release carrying **notes and the tag only**. Nothing is attached to it, and no Actions
artifact is uploaded either.

That is deliberate. Repilot is a paid app in a public repo, and Actions artifacts on a
public repo can be downloaded by anyone with read access, which is everyone. An unpackaged
build would not be a usable product to hand out in any case: the Copilot key is claimed
through the `com.microsoft.windows.copilotkeyprovider` AppExtension in
`Package.appxmanifest`, so without package identity the key cannot be assigned at all (the
Home page hides the Assign button and says so). The Store is the install route; anyone who
wants to run it from source can build it with the commands above. There is no portable build.

The workflow still **builds the MSIX for x64 and ARM64 on every run**, even though nothing
consumes the result. It is the only automated check that manifest stamping, `makepri`,
`makeappx` and signing still work, and a break in those would otherwise surface for the
first time during a Store submission.

### Publishing credentials

Use the four repository secrets listed above. The Entra tenant and application IDs identify
the app linked in Partner Center; the secret is its client-secret value, and `SELLER_ID`
comes from Partner Center account settings.

To create the linked app: Partner Center > Account settings > User management >
Azure AD applications > Add Azure AD application. Give it the Manager role, then create
its client secret in Entra. Renew the secret before expiry.
