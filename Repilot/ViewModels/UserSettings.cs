using CommunityToolkit.Mvvm.ComponentModel;
using Repilot.Models;

namespace Repilot.ViewModels;

/// <summary>
/// User settings for Repilot. All [ObservableProperty] fields generate
/// INotifyPropertyChanged via CommunityToolkit.Mvvm source generators and
/// auto-serialize to settings.json.
/// </summary>
public partial class UserSettings : ObservableObject
{
    /// <summary>App theme. 0 = System default, 1 = Light, 2 = Dark.</summary>
    [ObservableProperty]
    public partial int AppTheme { get; set; }

    /// <summary>
    /// Settings-app language tag (for example "fr-CA"), or "" to follow Windows.
    /// Applied at startup by <see cref="Repilot.Classes.AppLanguage"/>.
    /// </summary>
    [ObservableProperty]
    public partial string LanguageOverride { get; set; } = "";

    /// <summary>The action run when the Copilot key is pressed.</summary>
    public CopilotActionData Action { get; set; } = new();

    /// <summary>Last known app version string.</summary>
    [ObservableProperty]
    public partial string LastKnownVersion { get; set; } = "";

    // ── Settings window geometry ─────────────────────────────────────
    [ObservableProperty] public partial int SettingsWindowX { get; set; }
    [ObservableProperty] public partial int SettingsWindowY { get; set; }
    [ObservableProperty] public partial int SettingsWindowWidth { get; set; }
    [ObservableProperty] public partial int SettingsWindowHeight { get; set; }

    /// <summary>Repairs any nulls from older/partial settings files.</summary>
    public void CompleteInitialization()
    {
        Action ??= new CopilotActionData();
        LanguageOverride ??= "";
    }
}
