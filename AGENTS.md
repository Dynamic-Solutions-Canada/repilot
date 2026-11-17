# Repilot: Agent Instructions

Repilot lets Windows 11 users choose what the Copilot key does: a keyboard shortcut, typed text, an app, or a Windows function. It is a paid Microsoft Store app (product `9PB5FJ08PNVJ`) that claims the key through Microsoft's official Copilot hardware-key provider model, so it only works as a packaged MSIX. It is built from WSL through the shared Windows build bridge, and its "Our other apps" page comes from the apps-promo submodule at `external/promo`.

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
| Handler: gesture, debounce, log | `RepilotKey/Program.cs` |
| Settings window and pages | `SettingsWindow.xaml(.cs)`, `Pages/*` |
| Key assignment status | `Services/CopilotKeyProvider.cs` (`HandlerAppId = "App"`) |
| Action summary, installed apps, function text | `Services/ActionSummary.cs`, `Services/InstalledAppsService.cs`, `Services/WindowsFunctionText.cs` |
| Settings | `ViewModels/UserSettings.cs`, `Classes/Settings/SettingsManager.cs` |
| Repository name, used by About and the update check | `Classes/AboutContent.cs` |
| Update check | `Services/UpdateService.cs` |
| Interface text (nine languages) | `Strings/<lang>/Resources.resw`, `Services/UiStrings.cs`, `Classes/AppLanguage.cs` |
| "Our other apps" page | `external/promo` submodule; English-copy adapter in `Pages/PromotedAppsPage.Localization.cs` |

## Behaviour

- **The provider (`Id="App"`) must stay in the app list; never set `AppListEntry="none"` on it.** The "Customize Copilot key" picker offers only visible, launchable apps. A hidden provider still registers in the AppExtension catalog, so a catalog probe finds it, but Settings rejects it with "your PC doesn't have an app that meets the hardware key criteria". Both Start tiles are therefore required; the second one is not a bug. (This shipped broken in 1.0.13 and was fixed in 1.0.14.)
- **One press runs the action once.** Holding the key auto-repeats the launch, so the handler debounces with a `repeat-state` timestamp and the named mutex `Repilot.HandlerState`. A press-and-hold activation (`repilot-key://?state=Up`) is a no-op: Windows does not reliably deliver a distinct hold gesture, so hold was dropped.
- **`ActionExecutor`** sends key combos with `SendInput` (reusing a physically held Win key when triggered by Win+C), starts apps, URIs and `ms-settings:` with `Process.Start(UseShellExecute)`, and starts packaged apps through `explorer.exe shell:AppsFolder\{AUMID}`.
- **Unpackaged builds cannot be assigned to the key**, because the key is claimed through the `com.microsoft.windows.copilotkeyprovider` AppExtension. `Pages/HomePage.xaml.cs` hides the Assign button and says so.
- **Check for Updates** opens the Store's Updates and downloads page for packaged copies, without claiming a scan finished. Unpackaged builds check GitHub Releases and offer "View Release", never a download.

## Conventions and gotchas

- **Linked shared files stay WinUI-free and AOT/trim-safe:** no NLog, no reflection-heavy code. Handler errors go to `ActionExecutor.ErrorSink` in the settings app, or to the handler's own `key-handler.log`.
- **Both executables resolve the same settings path,** `%AppData%\Repilot\settings.json`, which Windows redirects to package data when packaged.
- **Interface text uses `.resw`** (LOC-6), in `Repilot/Strings/<lang>/Resources.resw` for the seven distinct wordings; `build-msix.ps1` packages en-CA and fr-CA as copies of en-US and fr-FR. The shared locale rules are in [locales.md](https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/standards/locales.md). The manifest, the picker (`Classes/AppLanguage.cs`) and the test's language list must agree.
- **XAML uses `x:Uid`** (`Name.Property` entries) and code uses `UiStrings.Get/Format`. Never give a plain code key the same name as an `x:Uid`: Windows turns the dot into a path, so `A` and `A.Content` collide.
- **Stored data never changes language.** Settings keep function Ids and `WindowsFunctionText` translates at display time. The catalog stays English because the handler links it, and the handler shows no text, so nothing localization-related goes into `RepilotKey/` or the linked files.
- **A language change needs the settings app reopened.** The choice is applied in the `App` constructor through `ApplicationLanguages.PrimaryLanguageOverride`, before any XAML loads; there is no live relabelling.
- **Packaging strips makepri's language splitting,** because split PRIs are dropped from a flat MSIX and every translation would be lost silently. The build fails unless a probe string resolves in every declared language.
- **`external/promo` is never edited.** `Pages/PromotedAppsPage.Localization.cs` translates exact known English copy from it; unknown remote copy stays English.
- **PowerShell scripts must be ASCII.** Windows PowerShell 5.1 reads a BOM-less `.ps1` as ANSI, so a UTF-8 dash inside a string becomes a curly quote and breaks parsing.
- `global::` does not parse inside interpolated strings. Fully qualify `Microsoft.UI.Xaml.Visibility` and `FocusState` in page code-behind.
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
| `npm test` | Every harness under `tests/` passes on Linux | What needs Windows, the installed package or a key press (see each README) |
| `npm run validate` | The Linux gate: format, `.gitignore` shared block and l10n checks, then `npm test` | A Windows compile; also run `verify:desktop` |
| `npm run format` | Rewrites C# outside `external/` to the kit's style | Behaviour |

`tests-native/Repilot.InputTests` sends real keystrokes: never part of `test`, `validate` or CI; Windows only, with `REPILOT_INPUT_TESTS=1` (see its README).

Before finishing a change, run `npm run validate` and `npm run verify:desktop`; use the `desktop` skill for builds and installs.

## Release

Repilot ships only through the Microsoft Store; a GitHub Release carries notes and the tag, nothing downloadable ([DIST-5](https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/standards/distribution.md), and the header of `build-msix.yml`). That workflow still builds and signs the MSIX on both platforms as CI's only packaging check; keep the step. A `v*` tag makes `store-publish.yml` upload the Store package (`Tier1012`, US $0.99; manual runs default to draft review). Keep `build-msix.ps1 -NoSign` working for local Store packages. Bump `<Version>` in `Directory.Build.props` for releases only; dev builds use a staging-only revision.

## Guidance map

| Guide, doc, or skill | Read or use when | Governs |
| --- | --- | --- |
| [docs/development.md](docs/development.md) | Building, packaging, Store setup, or the CI workflows | Build setup, `.github/workflows/**` |
| [.agents/guides/ui-conventions.md](.agents/guides/ui-conventions.md) | Any window, page, card or setting row | `Repilot/**/*.xaml(.cs)` |
| [Windows build tools](https://github.com/Dynamic-Solutions-Canada/windows-build-tools) | A Windows build, install or packaging problem | `windows-build.json`, the kit's `scripts/windows-build` |
| `desktop` skill | Building, verifying or installing locally | Local Windows builds |

## Keeping docs current

| What changed | Update |
| --- | --- |
| A file, service or page added, moved or removed | The Architecture table above |
| A file linked into the handler | The Architecture table, and check it stays WinUI-free and trim-safe |
| A UI pattern or page layout | `.agents/guides/ui-conventions.md` |
| An npm command | The Commands table above, `docs/development.md`, the `desktop` skill, `paseo.json` |
| A Windows function | The catalog, every `Resources.resw` |
| A language | The manifest, `Classes/AppLanguage.cs`, the localization harness's list, `build-msix.ps1` |
| winui-app-kit pin bumped | The kit's `.editorconfig` if it changed, then `npm run format` |
| A feature or requirement users can see | Behaviour above, `README.md` |
| Release workflow or packaging | Release above, `docs/development.md` |
| A guide or skill moved or removed | The guidance map above and every skill that names it |
