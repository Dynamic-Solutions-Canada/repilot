# Developing Repilot

Agent instructions, including the architecture rules and gotchas, are in [AGENTS.md](../AGENTS.md).

## Architecture (two processes, by design)

The keypress path is deliberately thin and non-resident: nothing runs in the background just to wait for the key.

- **`RepilotKey.exe`** (`RepilotKey/`) is a small self-contained (ReadyToRun, trimmed, single-file) executable. It is the registered Copilot key provider (Start tile **"Repilot Key"**). On a key press Windows launches it; it reads `settings.json`, performs the action (a synthesized keystroke via `SendInput`, or `Process.Start` for an app or URI) and exits. No window, no resident process, and a startup measured in milliseconds. (One csproj flag turns on full Native AOT if the VC++ build tools are present.)
- **`Repilot.exe`** (`Repilot/`) is the WinUI 3 settings app (Start tile **"Repilot"**). It is launched on demand to configure the action and check for updates, and is never resident or in the keypress path.

Both ship in one MSIX (`RepilotMSIX/`) with two `<Application>` entries:

- `Id="App"` is the handler. It is named "App" so the user's key assignment AUMID (`...!App`) survives updates.
- `Id="Settings"` is the WinUI app.

> **The provider (`Id="App"`) must be listed in the app list; do not set `AppListEntry="none"` on it.** The "Customize Copilot key" picker only offers visible, launchable apps. A hidden provider still registers in the AppExtension catalog, but Settings rejects it as "no app meets the hardware key criteria". That is why both apps have a Start tile.

The handler and the settings app share their model, catalog and executor code through linked source files, so those files stay WinUI-free and AOT/trim-safe (no NLog, no reflection-heavy code).

## Requirements

- Windows 11 (build 22000+)
- Stable .NET 10 SDK 10.0.403 or newer, selected by `global.json`
- Windows SDK packaging tools to build the MSIX
- Full Native AOT for the handler additionally needs the VC++ build tools (optional)

## Building

After cloning, run `git submodule update --init` (winui-app-kit supplies the build, test and format scripts) and, once per clone, `lefthook install` to enable the git hooks in `lefthook.yml` (they block signing material, credentials, settings files and files over 5 MB).

Source is authoritative in WSL. Builds run through the [shared Windows build tools](https://github.com/Dynamic-Solutions-Canada/windows-build-tools), which copy the working tree into a disposable Windows temporary folder, build there and clean up. Do not build a Windows-side checkout, or build the WSL source directly with Windows tools. Dev installs change the version only in the temporary copy; never uninstall to get past an update error.

| Command | What it does |
| --- | --- |
| `npm run verify:desktop` | Compiles the settings app (Debug), XAML included |
| `npm run package:desktop:dev` | Builds a signed dev package (sideload) |
| `npm run install:desktop` | Builds the dev package, updates the installed copy in place and launches it |
| `npm run package:desktop` | Builds the unsigned Store package |

The Linux checks need no Windows machine. They run through winui-app-kit's `scripts/dotnet`, which uses a host .NET SDK if there is one and otherwise the SDK container pinned by `global.json`, under a 3 GB memory cap (Docker required).

| Command | What it does |
| --- | --- |
| `npm run validate` | The whole Linux gate: `format:check`, `check:gitignore`, `check:l10n`, then `npm test` |
| `npm test` | Runs every harness under `tests/` (found by walking the folder) |
| `npm run check:l10n` | The localization harness (`tests/Repilot.Localization.Tests`) |
| `npm run check:gitignore` | Checks `.gitignore` ends with the kit's shared block unchanged |
| `npm run format` / `format:check` | Formats C# outside `external/` to the kit's `.editorconfig`, or fails on a difference |

`tests-native/Repilot.InputTests` sends real keystrokes, so it never runs from these commands or CI; its README says how to run it deliberately on Windows.

The packaging script publishes both executables (the self-contained WinUI app and the handler) into the package. It finds the Windows SDK packaging tools (makeappx, makepri, signtool) in an installed SDK or the Microsoft.Windows.SDK.BuildTools NuGet package, acquiring it if needed. A signed build needs a certificate passed to it, which the build tools do; it never makes its own. Bump `<Version>` in `Directory.Build.props` for releases only; the build tools allocate dev revisions in the temporary copy.

Run build commands from the repository or its project directories so `global.json` selects the SDK. It requires stable .NET SDK 10.0.403 or a later 10.0 feature band or patch, and both packaging workflows install the version from that file. The SDK still chooses runtime packs for each architecture; `Directory.Build.targets` rejects resolved .NET 10 runtime packs older than 10.0.14, including explicit overrides.

This is the minimum build baseline for [CVE-2026-111497](https://github.com/dotnet/runtime/security/advisories/GHSA-p3jp-9v83-76w9). It does not establish that Repilot processes attacker-controlled Brotli input. Both executables in the MSIX are self-contained, so an existing package needs a fresh restore, rebuild and Store update to receive the runtime fix. Updating the machine's shared runtime alone does not update either packaged executable.

## App language

The settings app follows the Windows display language by default. **Settings > App language** saves an override; close and reopen the settings app to apply it. Strings are in `Repilot/Strings/<lang>/Resources.resw`; see the conventions in [AGENTS.md](../AGENTS.md) for the rules, and run `npm run check:l10n` after changing any text.

## Where Repilot writes data

Repilot collects no telemetry or crash reports. Everything it writes stays on the computer.

| What | Where | Written by | After uninstall |
| --- | --- | --- | --- |
| Settings (the action, theme, language) | `%AppData%\Repilot\settings.json` | Settings app | Removed (see below) |
| Key-repeat timestamp, for the one-press-one-run debounce | `%AppData%\Repilot\repeat-state` | Key handler | Removed (see below) |
| Key handler error log | `%AppData%\Repilot\key-handler.log` | Key handler | Removed (see below) |
| Settings app logs | `%AppData%\Repilot\logs.<start time>.txt` | Settings app (NLog) | Removed (see below) |
| App language override | The package's own app data (`ApplicationLanguages.PrimaryLanguageOverride`) | Settings app | Removed with the package |
| Copilot key assignment | `HKCU\Software\Microsoft\Windows\Shell\BrandedKey` | Windows Settings, not Repilot (Repilot only reads it) | Owned by Windows |

In the packaged (MSIX) install, `%AppData%\Repilot` is redirected into the package's own folder, which Windows deletes on uninstall. The exception is a `%AppData%\Repilot` folder that already existed before the package was installed (for example from an unpackaged dev build): Windows writes to it in place, and it survives uninstall. An unpackaged dev build always writes to the real `%AppData%\Repilot`. Repilot writes nothing to the registry, creates no startup entry and runs no service.

This list is unconfirmed: it is taken from the code and has not yet been checked against a real uninstall.

To delete Repilot's data without uninstalling, close the settings app and delete `settings.json`; the next launch starts from defaults.

## Publishing to the Microsoft Store

The `store-publish.yml` workflow submits to product `9PB5FJ08PNVJ`.

The workflow pins [Microsoft Store CLI v0.4.3](https://github.com/microsoft/msstore-cli/releases/tag/v0.4.3). It builds unsigned x64 and ARM64 packages on one runner, combines them into an architecture-aware `.msixbundle` stamped with the app version, wraps that in a versioned `.msixupload`, and submits directly to Partner Center on `v*` tags. It uploads no public binary artifacts. MakeAppx must receive `/bv` so the bundle does not get a date-based version that could outrank the next app release. Manual dispatch defaults `no_commit` to true for draft review; disable it to commit the submission. Certification and the submission's publishing settings determine when it becomes available.

The published base price is US $0.99. The API may report it as `PriceId: "Base"`, which the CLI cannot round-trip, so the workflow explicitly supplies `Tier1012`, the US $0.99 tier identified by a [Microsoft maintainer](https://github.com/microsoft/msstore-cli/pull/175#issuecomment-5791491206). Tier pricing can change converted prices in other markets; review the ingested submission in Partner Center. The workflow checks for a pending submission before invoking the CLI, because the CLI would otherwise delete an existing draft.

The repository secrets are `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET`, and `SELLER_ID`. All four were present when checked on September 22, 2026. The first tier-based submission accepted authentication and Tier1012, but a ZIP of loose MSIX packages ingested as x64 only. Submission 7 was withdrawn to draft before publication. The API reports it as `Canceled`, so it cannot be edited. Run `replace_canceled_draft` with its exact ID and `no_commit` disabled to remove only that canceled submission and publish a corrected bundle. Verify both architectures and pricing in Partner Center after ingestion. Future tag runs use the MSIX bundle format.

The corrected [Store run](https://github.com/RyanEwen/Repilot/actions/runs/35898180528) committed Submission 7 (`1152921505701962545`) with a 1.0.19.0 bundle covering x64 and ARM64. Partner Center shows US $0.99, with 36 of 240 regional prices changed from the published Base schedule. It is in certification and will publish automatically after passing.

**Releasing:** bump `<Version>` in `Directory.Build.props`, commit, create the matching `vX.Y.Z` tag and push. Verify both the notes-only release and Store submission workflows.

For a manual fallback, generate images and build both packages on Windows:

```powershell
.\RepilotMSIX\generate-msix-images.ps1
.\RepilotMSIX\build-msix.ps1 -Platform x64 -NoSign
.\RepilotMSIX\build-msix.ps1 -Platform ARM64 -NoSign
```

Upload the individual versioned `.msix` files from `RepilotMSIX/bin/msix-output` in Partner Center. The `.msixupload` container is used by CI's CLI submission.

### What a GitHub release contains

The tag runs [`Build and Package`](../.github/workflows/build-msix.yml), which publishes a release carrying **notes and the tag only**. Nothing is attached to it, and no Actions artifact is uploaded either.

That is deliberate. Repilot is a paid app in a public repo, and Actions artifacts on a public repo can be downloaded by anyone with read access, which is everyone. An unpackaged build would not be a usable product to hand out in any case: the Copilot key is claimed through the `com.microsoft.windows.copilotkeyprovider` AppExtension in `Package.appxmanifest`, so without package identity the key cannot be assigned at all (the Home page hides the Assign button and says so). The Store is the install route; anyone who wants to run it from source can build it with the commands above. There is no portable build.

The workflow still **builds the MSIX for x64 and ARM64 on every run**, even though nothing consumes the result. It is the only automated check that manifest stamping, `makepri`, `makeappx` and signing still work, and a break in those would otherwise surface for the first time during a Store submission. It signs with a throwaway certificate made for each run.

### Publishing credentials

Use the four repository secrets listed above. The Entra tenant and application IDs identify the app linked in Partner Center; the secret is its client-secret value, and `SELLER_ID` comes from Partner Center account settings.

To create the linked app: Partner Center > Account settings > User management > Azure AD applications > Add Azure AD application. Give it the Manager role, then create its client secret in Entra. Renew the secret before expiry.
