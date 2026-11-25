> **Read when:** changing what a key press does, the handler, `ActionExecutor`, the Windows function catalog, or the files linked into the handler.
> **Governs:** `RepilotKey/**`, `Repilot/Services/ActionExecutor.cs`, `Repilot/Services/WindowsFunctionCatalog.cs`, `Repilot/Models/**`

# Key handler

How a Copilot key press becomes an action, and the rules that keep that path fast and reliable. The process split and the provider rule are in `AGENTS.md`.

## Behaviour

- **One press runs the action once.** Holding the key auto-repeats the launch, so the handler debounces with a `repeat-state` timestamp and the named mutex `Repilot.HandlerState`. A press-and-hold activation (`repilot-key://?state=Up`) is a no-op: Windows does not reliably deliver a distinct hold gesture, so hold was dropped.
- **`ActionExecutor`** sends key combos with `SendInput` (reusing a physically held Win key when triggered by Win+C), starts apps, URIs and `ms-settings:` with `Process.Start(UseShellExecute)`, and starts packaged apps through `explorer.exe shell:AppsFolder\{AUMID}`.
- **Unpackaged builds cannot be assigned to the key** (the `com.microsoft.windows.copilotkeyprovider` AppExtension needs package identity); `Pages/HomePage.xaml.cs` hides the Assign button and says so.

## Linked files

- **Linked shared files stay WinUI-free and AOT/trim-safe:** no NLog, no reflection-heavy code. Handler errors go to `ActionExecutor.ErrorSink` in the settings app, or to the handler's own `key-handler.log`.
- **Both executables read `%AppData%\Repilot\settings.json`**, redirected to package data when packaged. `Action` is read by the handler too, so a change to its shape needs a migration step in `SettingsMigrations.cs`.

## Adding a Windows function

Add an entry to `Services/WindowsFunctionCatalog.All` (unique `Id`, `Name`, `Group`, `Description`, and a `Combo` or `ShellTarget`). The picker and the handler pick it up through the linked file. Add `Function_<id>_Name` and `Function_<id>_Description` (and `FunctionGroup_*` for a new group) to every `Resources.resw`; the localization test fails until they exist and the en-US text matches the catalog.
