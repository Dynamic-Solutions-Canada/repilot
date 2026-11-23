# Repilot: Agent Instructions

Repilot lets Windows 11 users choose what the Copilot key does: a keyboard shortcut, typed text, an app, or a Windows function. It is a paid Microsoft Store app (product `9PB5FJ08PNVJ`) that claims the key as an official Copilot hardware-key provider, so it only works as a packaged MSIX. It is built from WSL through the shared Windows build bridge, and its "Our other apps" page comes from the apps-promo submodule at `external/promo`.

## Standards

This repo follows the shared standards at https://github.com/Dynamic-Solutions-Canada/app-standards. Read the index for this stack, https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/stacks/windows.md, then the topic file for the area you are changing. If `external/standards/` exists, it is the same content pinned for offline use. Where this file differs from the standards, this file wins and says why.

## Architecture

Two processes ship in one MSIX (`RepilotMSIX/`). Keep the split: the keypress path must stay thin and non-resident.

- **`RepilotKey.exe`** (`RepilotKey/`) is the key handler. Windows launches it on each key press; `Program.cs` detects the gesture, reads `settings.json`, calls `ActionExecutor.RunSync` and exits. No window, nothing resident. It is self-contained, trimmed and single-file.
- **`Repilot.exe`** (`Repilot/`) is the WinUI 3 settings app, launched on demand. `SettingsWindow` is its only window. It is not resident and not in the keypress path.
- The manifest has two `<Application>`s. `Id="App"` is the handler (provider and protocol extension; Start tile "Repilot Key"); it is named `App` so the user's key assignment (`PFN!App`) survives updates. `Id="Settings"` is the settings app (Start tile "Repilot").

| Area | Files |
|---|---|
| Shared with the handler through linked source (`<Compile Include="..\Repilot\...">`) | `Models/KeyCombo.cs`, `Models/CopilotActionData.cs`, `Models/WindowsFunction.cs`, `Services/WindowsFunctionCatalog.cs`, `Services/ActionExecutor.cs` |
| Handler: gesture, debounce, log; its settings reader | `RepilotKey/Program.cs`, `RepilotKey/KeySettings.cs` |
| Settings window (the kit's `SettingsShell`) and pages | `SettingsWindow.xaml(.cs)`, `Pages/*`; About is the kit's page, filled by `Classes/AboutContent.cs` |
| Key assignment status | `Services/CopilotKeyProvider.cs` (`HandlerAppId = "App"`) |
| Action summary, installed apps, function text | `Services/ActionSummary.cs`, `Services/InstalledAppsService.cs`, `Services/WindowsFunctionText.cs` |
| Settings (the kit's versioned store, with Reset) | `ViewModels/UserSettings.cs`, `Classes/Settings/SettingsManager.cs`, `Classes/Settings/SettingsMigrations.cs` |
| About content, repository, privacy URL, notices path, update check | `Classes/AboutContent.cs`; notices generated into `Repilot/THIRD-PARTY-NOTICES.txt` |
| App icon and MSIX images | the icon masters `assets/brand/icon.svg` and `icon-mono.svg` with `icons.json`, from which `npm run icons` (devkit) writes `RepilotMSIX/Images/`, `Repilot.ico` and `Repilot.png` in `Repilot/Resources/`, and `assets/generated/` (committed output) |
| Interface text (nine languages) | `Strings/<lang>/Resources.resw`, `Services/UiStrings.cs`, `Classes/AppLanguage.cs` |
| "Our other apps" page | `external/promo` submodule; it follows the app's language through `PromoLocalization.LocaleProvider` (set in `App`) |

## Behaviour

- **The provider (`Id="App"`) must stay in the app list; never set `AppListEntry="none"` on it.** The "Customize Copilot key" picker offers only visible, launchable apps. A hidden provider still registers in the AppExtension catalog, but Settings rejects it ("your PC doesn't have an app that meets the hardware key criteria"). Both Start tiles are therefore required; the second one is not a bug. (This shipped broken in 1.0.13 and was fixed in 1.0.14.)
- **One press runs the action once.** Holding the key auto-repeats the launch, so the handler debounces with a `repeat-state` timestamp and the named mutex `Repilot.HandlerState`. A press-and-hold activation (`repilot-key://?state=Up`) is a no-op: Windows does not reliably deliver a distinct hold gesture, so hold was dropped.
- **`ActionExecutor`** sends key combos with `SendInput` (reusing a physically held Win key when triggered by Win+C), starts apps, URIs and `ms-settings:` with `Process.Start(UseShellExecute)`, and starts packaged apps through `explorer.exe shell:AppsFolder\{AUMID}`.
- **Unpackaged builds cannot be assigned to the key** (the `com.microsoft.windows.copilotkeyprovider` AppExtension needs package identity); `Pages/HomePage.xaml.cs` hides the Assign button and says so.
- **Check for Updates** opens the Store's Updates and downloads page for packaged copies, without claiming a scan finished. Unpackaged builds check GitHub Releases and offer "View Release", never a download.

## Conventions and gotchas

- **Linked shared files stay WinUI-free and AOT/trim-safe:** no NLog, no reflection-heavy code. Handler errors go to `ActionExecutor.ErrorSink` in the settings app, or to the handler's own `key-handler.log`.
- **Both executables read `%AppData%\Repilot\settings.json`**, redirected to package data when packaged.
- **`external/` is never edited** (KIT-8): change winui-app-kit or apps-promo in their own repos and bump the pin. The kit supplies the build props, logging, crash handling, theme, update check and the shared labels (`UiText`, configured in `App` for `kit.*` keys only).
- **Interface text is `.resw`, in nine languages;** read the localization guide before changing any text, language or the language picker.
- **PowerShell scripts must be ASCII.** Windows PowerShell 5.1 reads a BOM-less `.ps1` as ANSI, so a UTF-8 dash inside a string becomes a curly quote and breaks parsing.
- **Adding a Windows function:** add an entry to `Services/WindowsFunctionCatalog.All` (unique `Id`, `Name`, `Group`, `Description`, and a `Combo` or `ShellTarget`). The picker and the handler pick it up through the linked file. Add `Function_<id>_Name` and `Function_<id>_Description` (and `FunctionGroup_*` for a new group) to every `Resources.resw`; the localization test fails until they exist and the en-US text matches the catalog.

## Commands

Source is authoritative in WSL, with submodules initialised (`git submodule update --init`). The kit submodule supplies only scripts and `.editorconfig` so far. Setup is in [docs/development.md](docs/development.md).

| Command | Proves | Does not prove |
| --- | --- | --- |
| `npm run verify:desktop` | The settings app compiles on Windows (Debug), XAML included | That the handler publishes, or that it packages or runs |
| `npm run package:desktop:dev` | A signed dev package builds, handler included | That it installs or runs |
| `npm run install:desktop` | The dev package updates in place and launches | Key behaviour; check it in the running app |
| `npm run package:desktop` | An unsigned Store package builds, with every language resolving | Signing; that it runs |
| `npm run check:l10n` | Every language is complete and consistent with the XAML, code and catalog | That the text is translated well |
| `npm test` | Every harness under `tests/` passes on Linux | What needs Windows, the installed package or a key press |
| `npm run validate` | The Linux gate: format, `.gitignore` shared block, icon, notices, screenshot, listing and l10n checks, then `npm test` | A Windows compile; also run `verify:desktop` |
| `npm run icons` | Writes every app icon from `assets/brand/` (devkit; `check:icons` in `validate` fails when a committed icon differs from what the masters make) | How the icon looks; check it on a light and a dark taskbar |
| `npm run format` | Rewrites C# outside `external/` to the kit's style | Behaviour |

`tests-native/Repilot.InputTests` sends real keystrokes: never part of `test`, `validate` or CI; Windows only, with `REPILOT_INPUT_TESTS=1` (see its README).

Before finishing a change, run `npm run validate` and `npm run verify:desktop`; use the `desktop` skill for builds and installs.

## Release

Use the `release` skill; a `v*` tag fails without the committed `release-notes/` files. Repilot ships only through the Microsoft Store; a GitHub Release carries notes and nothing downloadable ([DIST-5](https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/standards/distribution.md); the header of `build-msix.yml` says why). It still builds and signs both packages as CI's only packaging check; keep that step. `store-publish.yml` calls the kit's Store workflow (`Tier1012`, US $0.99): a tag stops at a draft, and submitting it is the maintainer's manual run. Packaging is winui-app-kit's `build/build-msix.ps1`, driven by `windows-build.json` (`companions` for the key handler, `resw_dir`, `pri_probe_keys`, `extra_folders` for `Public`).

## Guidance map

| Guide, doc, or skill | Read or use when | Governs |
| --- | --- | --- |
| [docs/development.md](docs/development.md) | Building, packaging, Store setup, or the CI workflows | Build setup, `.github/workflows/**` |
| [.agents/guides/ui-conventions.md](.agents/guides/ui-conventions.md) | Any window, page, card or setting row | `Repilot/**/*.xaml(.cs)` |
| [.agents/guides/localization.md](.agents/guides/localization.md) | Changing interface text, a language or the picker | `Repilot/Strings/**`, `Classes/AppLanguage.cs`, `Services/UiStrings.cs` |
| [Windows build tools](https://github.com/Dynamic-Solutions-Canada/windows-build-tools) | A Windows build, install or packaging problem | `windows-build.json`, the kit's `scripts/windows-build` |
| `desktop` skill | Building, verifying or installing locally | Local Windows builds |
| `release` skill | Cutting a release | `Directory.Build.props`, `release-notes/**`, tags |
| [store-assets/microsoft/listings.md](store-assets/microsoft/listings.md) | Changing Store listing text or a claim it makes | `store-assets/microsoft/**` (`npm run check:listing`) |

## Keeping docs current

| What changed | Update |
| --- | --- |
| A file, service or page added, moved or removed | The Architecture table above |
| A file linked into the handler | The Architecture table; keep it WinUI-free and trim-safe |
| A UI pattern or page layout | `.agents/guides/ui-conventions.md` |
| An npm command | The Commands table above, `docs/development.md`, the `desktop` skill, `paseo.json` |
| A Windows function | The catalog, every `Resources.resw` |
| A setting's shape | A step in `SettingsMigrations.cs` and a scenario in `tests/Repilot.Settings.Tests/`; `Action` is read by the handler too |
| A language | `.agents/guides/localization.md`, the manifest, `Classes/AppLanguage.cs`, the localization harness's list |
| winui-app-kit pin bumped | The `uses:` tag in `store-publish.yml` and `credential-expiry.yml`; the kit's `.editorconfig` if it changed, then `npm run format` |
| A feature or requirement users can see | Behaviour above, `README.md`, `store-assets/microsoft/listings.json` and the claims in `listings.md` |
| A UI change that shows in a Store screenshot | Recapture it (`store-assets/README.md`) |
| Release workflow or packaging | Release above, the `release` skill, `docs/development.md` |
| A NuGet package added, removed or updated | `npm run generate:notices`, and commit the notices file |
| A guide or skill moved or removed | The guidance map above and every skill that names it |
