> **Read when:** adding or changing any window, page, card, setting row or InfoBar in the settings app.
> **Governs:** `Repilot/**/*.xaml`, `Repilot/**/*.xaml.cs`, `Repilot/Pages/**`, `Repilot/SettingsWindow.xaml(.cs)`

# UI conventions

This app's own interface conventions. The shared family rules are in the standards' [ui.md](https://github.com/Dynamic-Solutions-Canada/app-standards/blob/main/standards/ui.md); this file records only what is specific to Repilot as it stands today. Repilot does not use winui-app-kit's shared styles or settings shell yet; when it adopts them, this guide changes to point at them.

## Settings window

- `SettingsWindow` is the only window: a 48 px custom title bar (icon and "Repilot" in `CaptionTextBlockStyle`) above a left `NavigationView`.
- Menu items are Home, then Action. The footer holds "Our other apps", then About, then the built-in Settings item. Every item is labelled with `x:Uid` and navigated by its `Tag` (the page type name).
- Only the settings app has UI. The key handler (`RepilotKey/`) shows nothing, so no UI or text goes there or into the files it links.

## Pages

- A page is a `ScrollViewer` with `Padding="32,16,32,32"` holding a left-aligned `StackPanel` (`MaxWidth` 720 to 760), whose first child is the page title in `TitleTextBlockStyle`. The Action page is the exception: it is a `Grid` so the mode-specific panel can fill the remaining height.
- The Action page picks the action type with a `SelectorBar` (one `SelectorBarItem` per type, each with a `FontIcon`) and shows one panel per type below it.
- Status and notices are `InfoBar`s, not text blocks or dialogs: the key assignment status (with the Assign button as its `ActionButton`) on Home, and the language restart and "no background process" notes on Settings. They are not closable when they state a fact the user cannot dismiss.

## Cards and rows

- New cards copy the Home and Settings cards: a `Border` on `CardBackgroundFillColorDefaultBrush` with a 1 px `CardStrokeColorDefaultBrush` stroke, `CornerRadius="8"`, `Padding="16,12"` for a setting row (`20` for a content card), spaced by the page's `StackPanel`.
- A setting row is a two-column `Grid` (`ColumnSpacing="16"`): title (`FontWeight="SemiBold"`) and a wrapped description (`Opacity="0.7"`) on the left, the control on the right with `MinWidth="160"`.
- The About page's cards are an older generation (`CornerRadius="4"`, `Padding="16"`, bottom margin instead of spacing). Do not copy them; they are replaced when About moves to the kit's shared page.
- Button content with an icon is a horizontal `StackPanel` (`Spacing="8"`) with a 14 px `FontIcon`, then the label. The primary action on a card uses `AccentButtonStyle`.

## Text

- XAML text uses `x:Uid`; code-built text uses `UiStrings.Get/Format`. Never hardcode user-visible English, except the product name.
- Language options are labelled in their own language and filled in code.
- A language change needs the window reopened; the Settings page says so in its restart InfoBar. Do not try to relabel pages live.

## Code-behind gotchas

- `global::` does not parse inside interpolated strings; assign to a local first.
- Fully qualify `Microsoft.UI.Xaml.Visibility` and `FocusState` in page code-behind.
