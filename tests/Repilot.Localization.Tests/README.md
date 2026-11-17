# Localization check

A portable console harness (TEST-2) for the settings app's interface text. It runs on Linux and in CI, with no Windows machine, and exits non-zero if any check fails.

```bash
dotnet run --project tests/Repilot.Localization.Tests -c Release
```

## What it checks

- **Every language is complete:** each `Repilot/Strings/<lang>/Resources.resw` has exactly the English keys, with no duplicates, no empty values, no em dashes and the same placeholders as English.
- **Keys cannot collide:** no plain code key has the same name as an `x:Uid` prefix (Windows turns `A.B` into the path `A/B`).
- **XAML matches the strings:** every `x:Uid` is used once, has resources, and sets only properties its element has; every resource is used.
- **Code matches the strings:** every `UiStrings.Get/Format` key and every key the promo adapter uses exists, and no resource is unused.
- **The Windows function catalog matches:** each catalog entry has its `Function_<id>_Name` and `_Description` strings, the en-US text equals the catalog, and no catalog string is orphaned.
- **Languages agree:** the language picker, the manifest and the string folders declare the same set (en-CA and fr-CA are packaged from en-US and fr-FR).
- **English summaries:** the action summaries the settings app shows read as expected, including text that contains braces.

## What it does not prove

- That the translations are good. They are machine-generated and unreviewed.
- That Windows resolves every language in the installed package. The packaging script's probe string checks that, at build time.
- Anything about the key handler, which shows no text.
