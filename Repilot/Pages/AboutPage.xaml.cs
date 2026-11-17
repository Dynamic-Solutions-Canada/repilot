using Repilot.Classes;
using Repilot.Services;
using WinUIAppKit;
using WinUIAppKit.Updates;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Repilot.Pages;

public sealed partial class AboutPage : Page
{
    private UpdateCheckResult? _update;

    public AboutPage()
    {
        InitializeComponent();

        var v = typeof(AboutPage).Assembly.GetName().Version!;
        VersionText.Text = UiStrings.Format("Version", $"{v.Major}.{v.Minor}.{v.Build}");
        CheckUpdateButton.Content = UiStrings.Get("CheckUpdates");
        RepositoryLink.NavigateUri = AboutContent.Repository;
    }

    /// <summary>
    /// Opens the Store's updates page when packaged; an unpackaged build checks GitHub Releases and,
    /// when a newer release exists, turns the button into "View release".
    /// </summary>
    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        bool packaged = AppEnvironment.IsPackaged;
        if (!packaged)
        {
            CheckUpdateButton.IsEnabled = false;
            CheckUpdateButton.Content = UiStrings.Get("Checking");
            UpdateStatusText.Text = UiStrings.Get("CheckingUpdates");
        }

        UpdateCheckResult result = await AboutContent.Updates.CheckForUpdatesAsync();
        switch (result.Outcome)
        {
            case UpdateCheckOutcome.OpenedStore:
                UpdateStatusText.Text = UiStrings.Get("StoreOpened");
                return;

            case UpdateCheckOutcome.UpdateAvailable:
                _update = result;
                UpdateStatusText.Text = UiStrings.Format("UpdateAvailable", result.LatestVersion ?? "", result.CurrentVersion);
                CheckUpdateButton.Content = UiStrings.Get("ViewRelease");
                CheckUpdateButton.Click -= CheckForUpdates_Click;
                CheckUpdateButton.Click += ViewRelease_Click;
                CheckUpdateButton.IsEnabled = true;
                return;

            case UpdateCheckOutcome.UpToDate:
                UpdateStatusText.Text = UiStrings.Format("UpToDate", result.CurrentVersion);
                break;

            default:
                // Failed: the Store would not open, or GitHub could not be read.
                UpdateStatusText.Text = UiStrings.Get(packaged ? "StoreOpenFailed" : "UpdateCheckFailed");
                break;
        }

        CheckUpdateButton.Content = UiStrings.Get("CheckUpdates");
        CheckUpdateButton.IsEnabled = true;
    }

    private void ViewRelease_Click(object sender, RoutedEventArgs e)
    {
        if (_update is not null)
        {
            UpdateService.OpenReleasePage(_update);
        }
    }
}
