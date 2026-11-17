using Repilot.Classes;
using Repilot.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Repilot.Pages;

public sealed partial class AboutPage : Page
{
    private UpdateService.UpdateCheckResult? _update;

    public AboutPage()
    {
        InitializeComponent();

        var v = typeof(AboutPage).Assembly.GetName().Version!;
        VersionText.Text = UiStrings.Format("Version", $"{v.Major}.{v.Minor}.{v.Build}");
        CheckUpdateButton.Content = UiStrings.Get("CheckUpdates");
        RepositoryLink.NavigateUri = AboutContent.Repository;
    }

    /// <summary>Checks GitHub releases for an unpackaged development build.</summary>
    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        if (UpdateService.IsPackaged)
        {
            UpdateStatusText.Text = UpdateService.OpenStoreUpdates()
                ? UiStrings.Get("StoreOpened")
                : UiStrings.Get("StoreOpenFailed");
            return;
        }

        CheckUpdateButton.IsEnabled = false;
        CheckUpdateButton.Content = UiStrings.Get("Checking");
        UpdateStatusText.Text = UiStrings.Get("CheckingUpdates");

        var result = await UpdateService.CheckForUpdateAsync();
        if (result == null)
        {
            UpdateStatusText.Text = UiStrings.Get("UpdateCheckFailed");
        }
        else if (result.UpdateAvailable)
        {
            _update = result;
            UpdateStatusText.Text = UiStrings.Format("UpdateAvailable", result.LatestVersion, result.CurrentVersion);
            CheckUpdateButton.Content = UiStrings.Get("ViewRelease");
            CheckUpdateButton.Click -= CheckForUpdates_Click;
            CheckUpdateButton.Click += ViewRelease_Click;
            CheckUpdateButton.IsEnabled = true;
            return;
        }
        else
        {
            UpdateStatusText.Text = UiStrings.Format("UpToDate", result.CurrentVersion);
        }

        CheckUpdateButton.Content = UiStrings.Get("CheckUpdates");
        CheckUpdateButton.IsEnabled = true;
    }

    private void ViewRelease_Click(object sender, RoutedEventArgs e)
    {
        if (_update?.ReleaseUrl is { Length: > 0 } url)
            UpdateService.OpenUrl(url);
    }
}
