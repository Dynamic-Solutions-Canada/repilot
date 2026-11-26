> **Read when:** changing any interface text, adding or removing a language, or touching the language picker.
> **Governs:** `Repilot/Strings/**`, `Repilot/Classes/AppLanguage.cs`, `Repilot/Services/UiStrings.cs`, `Repilot/Services/WindowsFunctionText.cs`

# Localization

Repilot's own rules for its interface text. The shared locale rules (the nine-locale set, aliases, unreviewed translations) are in the standards' [locales.md](https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/standards/locales.md). Check every change with `npm run check:l10n`.

- **Interface text uses `.resw`** (LOC-6), in `Repilot/Strings/<lang>/Resources.resw` for the seven distinct wordings; the kit's packaging script (`resw_dir` in `windows-build.json`) packages en-CA and fr-CA from the en-US and fr-FR folders. The shared locale rules are in [locales.md](https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/standards/locales.md). The manifest, the picker (`Classes/AppLanguage.cs`) and the test's language list must agree.
- **XAML uses `x:Uid`** (`Name.Property` entries) and code uses `UiStrings.Get/Format`. Never give a plain code key the same name as an `x:Uid`: Windows turns the dot into a path, so `A` and `A.Content` collide.
- **Stored data never changes language.** Settings keep function Ids and `WindowsFunctionText` translates at display time. The catalog stays English because the handler links it, and the handler shows no text, so nothing localization-related goes into `RepilotKey/` or the linked files.
- **A language change needs the settings app reopened.** The choice is applied in the `App` constructor through `ApplicationLanguages.PrimaryLanguageOverride`, before any XAML loads; there is no live relabelling.
- **Packaging keeps one resources.pri** (the kit's script fails if makepri splits it, since split PRIs are dropped from a flat MSIX and every translation would be lost silently), and fails unless the probe string resolves in every declared language. The probe is `NavHome`, listed in `pri_probe_keys` in `windows-build.json`: renaming or removing that string means updating the probe in the same change.
- **The kit's shared labels** (`kit.*`, used by the Reset row and the reset notice) come from winui-app-kit's own catalogs through `UiText`, which `App` configures with the same language choice. Repilot never defines a `kit.` key, and its own text never goes through `UiText`.
- **The "Our other apps" page** translates itself; `App` points `PromoLocalization.LocaleProvider` at `UiText.Locale`.
- **German addresses the user as "du"**, like every app in the family: imperatives in the du form ("Wähle", "Drücke"), "dein" rather than "Ihr". The listing's German text follows the same rule.
- **Japanese wraps at any character in WinUI.** A `TextBlock` has no phrase-aware line breaking, so a long Japanese description can end a line inside a word ("キャンセ" / "ルします。"). Where that happens, join the phrase's characters with U+2060 WORD JOINER in its `.resw` value, as `ShortcutInstructions.Text` does: it is invisible, screen readers skip it, and the line then breaks only between phrases.
