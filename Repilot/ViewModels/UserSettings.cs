using Repilot.Models;
using WinUIAppKit.Theming;
using WinUIAppKit.Windowing;

namespace Repilot.ViewModels;

/// <summary>
/// User settings, saved as settings.json by <see cref="Classes.Settings.SettingsManager"/>. The kit's
/// store owns the file's <c>SchemaVersion</c>; never declare one here. A change to the shape needs a step
/// in <see cref="Classes.Settings.SettingsMigrations"/>. The key handler reads <see cref="Action"/> from
/// the same file with its own reader, so <see cref="Action"/>'s name and shape are a contract with it.
/// </summary>
public sealed class UserSettings
{
    /// <summary>The settings app's theme (stored as 0 = system, 1 = light, 2 = dark).</summary>
    public AppThemeChoice AppTheme { get; set; }

    /// <summary>
    /// Settings-app language identity (for example "fr-CA"), or "" to follow Windows. Applied at startup
    /// by <see cref="Classes.AppLanguage"/>.
    /// </summary>
    public string LanguageOverride { get; set; } = "";

    /// <summary>The action run when the Copilot key is pressed. Read by the key handler.</summary>
    public CopilotActionData Action { get; set; } = new();

    /// <summary>Last normal position and size of the settings window, or null for the default.</summary>
    public WindowPlacement? SettingsWindow { get; set; }
}
