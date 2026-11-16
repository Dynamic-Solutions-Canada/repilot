using Repilot.Classes.Settings;
using Repilot.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Repilot.Pages;

public sealed partial class HomePage : Page
{
    public HomePage()
    {
        InitializeComponent();
        ActionText.Text = ActionSummary.Describe(SettingsManager.Current.Action);
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        if (!CopilotKeyProvider.IsPackaged)
        {
            StatusBar.Severity = InfoBarSeverity.Informational;
            StatusBar.Title = UiStrings.Get("StatusUnpackaged");
            StatusBar.Message = UiStrings.Get("StatusUnpackagedDescription");
            AssignButton.Visibility = Visibility.Collapsed;
            return;
        }

        switch (CopilotKeyProvider.GetStatus())
        {
            case ProviderStatus.Assigned:
            case ProviderStatus.AssignedByPolicy:
                StatusBar.Severity = InfoBarSeverity.Success;
                StatusBar.Title = UiStrings.Get("StatusAssigned");
                StatusBar.Message = UiStrings.Get("StatusAssignedDescription");
                AssignButton.Content = UiStrings.Get("ChangeAssignment");
                break;
            case ProviderStatus.AssignedToOtherApp:
                StatusBar.Severity = InfoBarSeverity.Warning;
                StatusBar.Title = UiStrings.Get("StatusOtherApp");
                StatusBar.Message = UiStrings.Get("StatusChooseRepilot");
                break;
            case ProviderStatus.SetToSearch:
                StatusBar.Severity = InfoBarSeverity.Warning;
                StatusBar.Title = UiStrings.Get("StatusSearch");
                StatusBar.Message = UiStrings.Get("StatusChooseRepilot");
                break;
            default:
                StatusBar.Severity = InfoBarSeverity.Warning;
                StatusBar.Title = UiStrings.Get("StatusNotAssigned");
                StatusBar.Message = UiStrings.Get("StatusAssignInstructions");
                break;
        }
    }

    private void AssignButton_Click(object sender, RoutedEventArgs e)
        => CopilotKeyProvider.OpenAssignmentSettings();

    private void ChangeButton_Click(object sender, RoutedEventArgs e)
        => SettingsWindow.GetCurrent()?.NavigateTo(typeof(ActionPage));

    private void TestButton_Click(object sender, RoutedEventArgs e)
        => ActionExecutor.Run(SettingsManager.Current.Action);
}
