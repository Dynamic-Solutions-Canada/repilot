# Windows input regression checks

A native suite (TEST-10): it sends real keystrokes. It is never part of `npm test`, `npm run validate` or CI, and lives outside `tests/` so test discovery never finds it.

## Guard

It refuses to start, and exits with code 2, unless it is running on Windows and `REPILOT_INPUT_TESTS=1` is set. Set the variable only on a machine nobody is using, such as the Windows test VM. While it runs it opens and closes its own text field; keep that window focused. It also refuses to type into any other foreground window.

## Running it deliberately

Run it on Windows, in a disposable copy of the repository (the shared Windows build tools' temporary copy, or a VM), not against the WSL source directly:

```powershell
$env:REPILOT_INPUT_TESTS = "1"
dotnet run --project tests-native/Repilot.InputTests/Repilot.InputTests.csproj -c Release
Remove-Item Env:REPILOT_INPUT_TESTS
```

## What it covers

Source-generated settings serialization and existing shortcut settings, US and Portuguese key labels, real Unicode text delivery (including surrogate pairs and spaces), empty actions, and existing shortcut delivery. Keyboard layout changes apply only to the test thread and are restored afterward; user settings are never touched.

## What it does not prove

Physical Copilot key activation, or behaviour in every application. For an installed-package check, choose **Text**, enter `ç`, focus a text field in another app, and press the Copilot key. Check that one press inserts one character, and that the Text action survives closing and reopening Settings.
