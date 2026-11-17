using WinUIAppKit.Theming;
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
        ThemeCombo.SelectedIndex = (int)SettingsManager.Current.AppTheme;
        LoadLanguageChoices();
        SetUpReset();
        _loaded = true;
    }

    /// <summary>
    /// "Reset settings" (CONF-4): the kit's row asks first, then the store resets every Repilot setting,
    /// the Copilot key's action included. The page is rebuilt to show the defaults; a language change
    /// still needs a reopen, which the restart notice then says.
    /// </summary>
    private void SetUpReset()
    {
        ResetRow.Window = SettingsWindow.GetCurrent();
        ResetRow.ResetAsync = () =>
        {
            SettingsManager.Reset();
            return Task.CompletedTask;
        };
        ResetRow.KeptNote = () => UiStrings.Get("ResetKeptNote");
        ResetRow.ResetCompleted += () => Frame.Navigate(typeof(SettingsPage));
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loaded)
        {
            return;
        }

        // Every open window follows at once (AppTheme.Track); the choice is saved for the next launch.
        SettingsManager.Current.AppTheme = (AppThemeChoice)ThemeCombo.SelectedIndex;
        SettingsManager.Save();
        AppTheme.Set(SettingsManager.Current.AppTheme);
    }

    /// <summary>
    /// Fills the language list: "Follow Windows" (in the current language) first, then each
    /// supported language in its own language. Item tags hold the saved value ("" = Windows).
    /// </summary>
    private void LoadLanguageChoices()
    {
        LanguageCombo.Items.Add(new ComboBoxItem { Content = UiStrings.Get("FollowWindows"), Tag = "" });
        foreach (string identity in AppLanguage.Supported.Identities)
        {
            LanguageCombo.Items.Add(new ComboBoxItem { Content = AppLanguage.Supported.NativeName(identity), Tag = identity });
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
