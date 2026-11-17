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

Signed dev packages use their own identity, `TechnicallyReal.Repilot` (DIST-3), so a dev install sits beside the Store install and has its own data folder; assign the Copilot key to whichever copy you want to test. To test an update over a Store install instead, add `-- --store-identity` to `install:desktop` or `package:desktop:dev`: that package takes the Store identity (`27766TechnicallyReal.CopilotKeyRemapper`). Unsigned Store packages always keep the Store identity. All three are set in `windows-build.json`.

The packaging script publishes both executables (the self-contained WinUI app and the handler) into the package. It finds the Windows SDK packaging tools (makeappx, makepri, signtool) in an installed SDK or the Microsoft.Windows.SDK.BuildTools NuGet package, acquiring it if needed. A signed build needs a certificate passed to it, which the build tools do; it never makes its own. Bump `<Version>` in `Directory.Build.props` for releases only; the build tools allocate dev revisions in the temporary copy.

Run build commands from the repository or its project directories so `global.json` selects the SDK. It requires stable .NET SDK 10.0.403 or a later 10.0 feature band or patch, and both packaging workflows install the version from that file. The SDK still chooses runtime packs for each architecture; `Directory.Build.targets` rejects resolved .NET 10 runtime packs older than 10.0.14, including explicit overrides.

This is the minimum build baseline for [CVE-2026-111497](https://github.com/dotnet/runtime/security/advisories/GHSA-p3jp-9v83-76w9). It does not establish that Repilot processes attacker-controlled Brotli input. Both executables in the MSIX are self-contained, so an existing package needs a fresh restore, rebuild and Store update to receive the runtime fix. Updating the machine's shared runtime alone does not update either packaged executable.

## App language

The settings app follows the Windows display language by default. **Settings > App language** saves an override; close and reopen the settings app to apply it. Strings are in `Repilot/Strings/<lang>/Resources.resw`; see the conventions in [AGENTS.md](../AGENTS.md) for the rules, and run `npm run check:l10n` after changing any text.

## Where Repilot writes data

Repilot collects no telemetry or crash reports. Everything it writes stays on the computer.

| What | Where | Written by | After uninstall |
| --- | --- | --- | --- |
| Settings (the action, theme, language, window position) | `%AppData%\Repilot\settings.json` | Settings app (the kit's versioned store); the key handler reads it | Removed (see below) |
| Settings backups: before an upgrade (`settings.json.v<old>.bak`), an unreadable file (`settings.json.bad`), a reset (`settings.json.<UTC time>.reset`) | Beside `settings.json` | Settings app | Removed (see below) |
| Key-repeat timestamp, for the one-press-one-run debounce | `%AppData%\Repilot\repeat-state` | Key handler | Removed (see below) |
| Key handler error log | `%AppData%\Repilot\key-handler.log` | Key handler | Removed (see below) |
| Settings app logs (the current file and three older ones) | `%AppData%\Repilot\logs.<start time>.txt` | Settings app (the kit's shared NLog config) | Removed (see below) |
| App language override | The package's own app data (`ApplicationLanguages.PrimaryLanguageOverride`) | Settings app | Removed with the package |
| Copilot key assignment | `HKCU\Software\Microsoft\Windows\Shell\BrandedKey` | Windows Settings, not Repilot (Repilot only reads it) | Owned by Windows |

In the packaged (MSIX) install, `%AppData%\Repilot` is redirected into the package's own folder, which Windows deletes on uninstall. The exception is a `%AppData%\Repilot` folder that already existed before the package was installed (for example from an unpackaged dev build): Windows writes to it in place, and it survives uninstall. An unpackaged dev build always writes to the real `%AppData%\Repilot`. Repilot writes nothing to the registry, creates no startup entry and runs no service.

This list is unconfirmed: it is taken from the code and has not yet been checked against a real uninstall.

To delete Repilot's data without uninstalling, close the settings app and delete `settings.json`; the next launch starts from defaults.

## Upgrade checks

Run these before a release that changes settings, packaging or identity. The automated harness (`tests/Repilot.Settings.Tests`) covers the file itself; these cover the installed app.

On the Windows 11 test VM (dev identity), with the windows-build-tools app-test recipe:

1. Install the previous release's dev package, give it settings (an action, a theme, a language, a moved window) and close it.
2. Install the new dev package over it, without uninstalling.
3. Open Repilot: the action, theme, language and window position are kept, `settings.json` has the new `SchemaVersion`, and the old file is beside it as `settings.json.v<old>.bak`.
4. Open Repilot again while it is open: the first window comes forward and no second copy stays running. Close it: no `Repilot.exe` remains.
5. Settings > Reset settings: the question is in the app's language and says the Copilot key stays assigned; Cancel changes nothing.
6. Assign the key (or set `BrandedKey` as Settings would) and press it: the saved action runs.

On a machine with the Store version installed (the maintainer's acceptance check):

7. `npm run install:desktop -- --store-identity` over the Store install, then repeat steps 3, 5 and 6. The key stays assigned to Repilot without visiting Windows Settings again.
8. Let the Store update back over it when the release ships.

## Continuous integration

- **CI** (`ci.yml`) runs `npm run validate` on Linux for every push to `main` and every pull request. When app source, packaging or build configuration changed, it also compiles the settings app and the key handler on Windows for x64 and ARM64.
- **CodeQL** (`codeql.yml`) scans C# and the workflows, buildless on Linux, on push, pull request and weekly; the submodules are left to their own repos.
- **Build and Package** (`build-msix.yml`) runs on `v*` tags and by hand: the gate, then both signed packages, then (on a tag) the GitHub Release.
- **Publish to Microsoft Store** (`store-publish.yml`) and **Credential Expiry** (`credential-expiry.yml`) call winui-app-kit's reusable workflows. Their `uses:` tag must equal the `external/winui-app-kit` pin.

## Releasing

Use the `release` skill ([.agents/skills/release/SKILL.md](../.agents/skills/release/SKILL.md)). In short: write the notes for users in `release-notes/<version>.md`, plus one translation per other language (`<version>.<identity>.md` for es-ES, fr-FR, de-DE, zh-CN, pt-BR and ja-JP; each under 1,500 characters), and get them approved. Then bump `<Version>` in `Directory.Build.props` in the same commit (`Bump version to vX.Y.Z`), create an annotated `vX.Y.Z` tag and push both. The tag runs fail if the tag and the version differ or a notes file is missing or too long; they never fall back to commit subjects.

## Publishing to the Microsoft Store

`store-publish.yml` publishes product `9PB5FJ08PNVJ` through winui-app-kit's reusable Store workflow. A `v*` tag runs the gate, builds unsigned x64 and ARM64 packages with `RepilotMSIX/build-msix.ps1` (named in `windows-build.json`), bundles them into a versioned `.msixupload`, uploads it with the notes as "What's new", and stops at a draft. Submitting is a second, manual run of the workflow on the same tag with `submit_draft` set to the draft ID from the tag run's summary and `no_commit` unchecked. Manual runs can also do a `dry_run` (everything except Store calls, no secrets needed), repair a draft, or replace a canceled or failed submission. Packages are never uploaded as workflow artifacts.

The published base price is US $0.99. The API may report it as `PriceId: "Base"`, which the Store CLI cannot round-trip, so the workflow explicitly supplies `Tier1012`, the US $0.99 tier identified by a [Microsoft maintainer](https://github.com/microsoft/msstore-cli/pull/175#issuecomment-5791491206). Tier pricing can change converted prices in other markets; review the draft in Partner Center before submitting. Each listing locale gets the notes for its language (en-CA and fr-CA use the en-US and fr-FR files).

For a manual fallback, generate images and build both packages on Windows, then upload the versioned `.msix` files from `RepilotMSIX/bin/msix-output` in Partner Center:

```powershell
.\RepilotMSIX\generate-msix-images.ps1
.\RepilotMSIX\build-msix.ps1 -Platform x64 -NoSign
.\RepilotMSIX\build-msix.ps1 -Platform ARM64 -NoSign
```

History: Submission 7 (`1152921505701962545`) was the first tier-based submission with a bundle covering x64 and ARM64 (1.0.19.0, committed by this [Store run](https://github.com/RyanEwen/Repilot/actions/runs/35898180528)); an earlier ZIP of loose packages ingested as x64 only, which is why uploads are bundles.

### What a GitHub release contains

The tag runs [`Build and Package`](../.github/workflows/build-msix.yml), which publishes a release carrying **notes and the tag only**. Nothing is attached to it, and no Actions artifact is uploaded either.

That is deliberate. Repilot is a paid app in a public repo, and Actions artifacts on a public repo can be downloaded by anyone with read access, which is everyone. An unpackaged build would not be a usable product to hand out in any case: the Copilot key is claimed through the `com.microsoft.windows.copilotkeyprovider` AppExtension in `Package.appxmanifest`, so without package identity the key cannot be assigned at all (the Home page hides the Assign button and says so). The Store is the install route; anyone who wants to run it from source can build it with the commands above. There is no portable build.

The workflow still **builds the MSIX for x64 and ARM64 on every run**, even though nothing consumes the result. It is the only automated check that manifest stamping, `makepri`, `makeappx` and signing still work, and a break in those would otherwise surface for the first time during a Store submission. It signs with a throwaway certificate made for each run.

### Publishing credentials

The Store credentials are organisation secrets shared with the other Store apps and visible to this repo: `AZURE_AD_TENANT_ID`, `AZURE_AD_APPLICATION_CLIENT_ID`, `AZURE_AD_APPLICATION_SECRET` and `SELLER_ID`, passed to the kit's workflow with `secrets: inherit`. The tenant and application IDs identify the app linked in Partner Center; the secret is its client-secret value, and `SELLER_ID` comes from Partner Center account settings. The organisation variable `STORE_SECRET_EXPIRES` holds the secret's expiry date; `credential-expiry.yml` fails from 60 days before it. Update it whenever the secret is rotated: `gh variable set STORE_SECRET_EXPIRES --org Dynamic-Solutions-Canada --body YYYY-MM-DD`.

To create the linked app: Partner Center > Account settings > User management > Azure AD applications > Add Azure AD application. Give it the Manager role, then create its client secret in Entra. Renew the secret before expiry.
