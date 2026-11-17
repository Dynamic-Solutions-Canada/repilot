using Repilot.Classes;
using Repilot.Classes.Settings;
using Repilot.Services;
using Microsoft.UI.Xaml.Controls;

namespace Repilot.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _loaded;

    public SettingsPage()
    {
        InitializeComponent();
        ThemeCombo.SelectedIndex = SettingsManager.Current.AppTheme;
        LoadLanguageChoices();
        _loaded = true;
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        ThemeManager.ApplyAndSaveTheme(ThemeCombo.SelectedIndex);
    }

    /// <summary>
    /// Fills the language list: "Follow Windows" (in the current language) first, then each
    /// supported language in its own language. Item tags hold the saved value ("" = Windows).
    /// </summary>
    private void LoadLanguageChoices()
    {
        LanguageCombo.Items.Add(new ComboBoxItem { Content = UiStrings.Get("FollowWindows"), Tag = "" });
        foreach (var option in AppLanguage.Supported)
        {
            LanguageCombo.Items.Add(new ComboBoxItem { Content = option.NativeName, Tag = option.Tag });
        }

        string saved = AppLanguage.Normalize(SettingsManager.Current.LanguageOverride);
        LanguageCombo.SelectedItem = LanguageCombo.Items
            .OfType<ComboBoxItem>()
            .First(item => (string)item.Tag == saved);

        ShowRestartNotice(saved != AppLanguage.AppliedAtStartup);
    }

    /// <summary>Saves the choice for the next launch; WinUI cannot relabel an open window.</summary>
    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        if (LanguageCombo.SelectedItem is not ComboBoxItem { Tag: string tag })
        {
            return;
        }

        bool needsRestart = AppLanguage.SaveChoice(tag);
        ShowRestartNotice(needsRestart);
    }

    private void ShowRestartNotice(bool show)
    {
        LanguageRestartBar.Message = UiStrings.Get("LanguageRestartPending");
        LanguageRestartBar.IsOpen = show;
    }
}
