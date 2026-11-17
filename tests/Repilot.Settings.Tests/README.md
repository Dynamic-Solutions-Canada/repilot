# Settings file harness

Checks how settings.json loads, upgrades and resets (CONF-3, CONF-4), and that the key handler can still read every file the settings app writes. It links winui-app-kit's real `JsonSettingsStore` and `AtomicFile`, the app's real schema steps (`SettingsMigrations.cs`), settings class (`UserSettings.cs`) and action model (`Models/`), and the handler's own reader (`RepilotKey/KeySettings.cs`), and runs them against real files in a temporary folder. The kit's theme and window types are the stand-ins in `Stubs.cs`.

Covers: a first run; a file written by 1.0.22 for each action type (shortcut, app with arguments, Windows function, text with a surrogate pair, nothing), each keeping its action, theme, language and window position, dropping the unused values, kept as `settings.json.v0.bak`, not reported as a first run, and read back by the handler unchanged; a 1.0.22 file with no saved window size; an unreadable file (kept as `.bad`, defaults written, reset notice due); a current file (no backup); a file from a newer version (backed up, loaded as far as this version understands); and Reset (old file kept as `.reset`, the action back to nothing, theme and language following Windows).

Does not prove: what the settings app does with each outcome (the reset notice on the Home page, the Reset button and its confirmation), that a packaged install upgrades in place, or that the Copilot key still runs the action afterwards. Those need the installed app; see the upgrade checks in [docs/development.md](../../docs/development.md).

Run with `npm test`, or alone with `external/winui-app-kit/scripts/dotnet run --project tests/Repilot.Settings.Tests`.
