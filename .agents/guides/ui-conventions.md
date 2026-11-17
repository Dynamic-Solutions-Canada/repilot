> **Read when:** adding or changing any window, page, card, setting row or InfoBar in the settings app.
> **Governs:** `Repilot/**/*.xaml`, `Repilot/**/*.xaml.cs`, `Repilot/Pages/**`, `Repilot/SettingsWindow.xaml(.cs)`, `Repilot/Classes/AboutContent.cs`

# UI conventions

This app's own interface conventions. The shared family rules are in the standards' [ui.md](https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/standards/ui.md), and the kit's styles, shell, rows and About page are documented in the [WinUI App Kit README](../../external/winui-app-kit/README.md); this file records only what is specific to Repilot.

## Settings window

- `SettingsWindow` hosts the kit's `SettingsShell` and is the only window. Repilot adds two menu items in code, Home then Action; the footer ("Our other apps", About, Settings) is the shell's. Never add footer items or restyle the shell.
- Tags map to pages in `SettingsWindow.PageFor`. About is the kit's `AboutPage`, opened with `AboutContent.Create()`; product cards go in its `ExtraCards` ("How it works" today), never in a page of Repilot's own.
- `WindowChrome.SetUpSettingsWindow` gives Mica, the title bar, icons, the minimum size, the saved placement and the theme. Only the key handler (`RepilotKey/`) has no UI.

## Pages

- A page is `ScrollViewer`, then `StackPanel Style="{StaticResource PageContentStyle}"`, a `PageTitleStyle` title, then sections: a `FirstSectionHeaderStyle` or `SectionHeaderStyle` heading above a `CardBorderStyle` card. Use the kit's keys from `Styles.xaml`; never write padding, radius or header sizes inline.
- The Action page is the exception: a `Grid` with the same padding and title style, so the app and function lists can fill the window. It picks the action type with a `SelectorBar`.
- Secondary text uses `TextFillColorSecondaryBrush` (with `CaptionTextBlockStyle` for notes under a control), never `Opacity`.
- Status and notices are `InfoBar`s: the key assignment status (with Assign as its action button) on Home, the language restart and "no background process" notes on Settings, and the kit's `SettingsResetNotice` inserted once at the top of Home after an unreadable settings file.
- The Settings page uses the kit's `ThemeSetting` and `ResetSetting`. Language stays Repilot's own combo box (restart-based, `.resw`), styled like the kit's rows: a header, then a caption.

## Buttons

- Button content with an icon is a `StackPanel Style="{StaticResource IconLabelStyle}"` with a `FontIcon Style="{StaticResource ButtonGlyphStyle}"`, then the label. The primary action on a card uses `AccentButtonStyle`.
- Such a button has no accessible name of its own: give it an `x:Uid` with a `[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name` entry equal to the label, in every `Resources.resw` (the localization check allows that property on `Button`).

## Text

- XAML text uses `x:Uid`; code-built text uses `UiStrings.Get/Format`. Never hardcode user-visible English, except the product name. The kit's own labels (`kit.*`) come through `l10n:Localize` and `UiText`; see the localization guide.

## Code-behind gotchas

- `global::` does not parse inside interpolated strings; assign to a local first.
- Fully qualify `Microsoft.UI.Xaml.Visibility` and `FocusState` in page code-behind, and the kit's About page as `WinUIAppKit.Pages.AboutPage`.
