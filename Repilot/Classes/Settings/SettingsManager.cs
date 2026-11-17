using Repilot.ViewModels;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Repilot.Classes.Settings;

/// <summary>
/// Manages app settings, serialized as JSON to %AppData%\Repilot\settings.json.
/// </summary>
public static class SettingsManager
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    internal static string SettingsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Repilot");

    private static string SettingsFilePath => Path.Combine(SettingsDir, "settings.json");

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        PropertyNamingPolicy = null, // PascalCase to match property names
    };

    private static UserSettings _current = new();

    /// <summary>The current user settings.</summary>
    public static UserSettings Current
    {
        get => _current ??= new UserSettings();
        set => _current = value;
    }

    /// <summary>Restores settings from disk (or returns defaults).</summary>
    public static UserSettings RestoreSettings(string? filePath = null)
    {
        filePath ??= SettingsFilePath;
        try
        {
            if (File.Exists(filePath))
            {
                string json = File.ReadAllText(filePath);
                var deserialized = JsonSerializer.Deserialize<UserSettings>(json, JsonOptions);
                if (deserialized != null)
                {
                    _current = deserialized;
                    _current.CompleteInitialization();
                    Logger.Info("Settings restored");
                    return _current;
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error restoring settings");
        }

        Logger.Warn("Settings not found or unreadable — loading defaults");
        _current = new UserSettings();
        _current.CompleteInitialization();
        return _current;
    }

    /// <summary>Saves the current settings to disk.</summary>
    public static void SaveSettings(string? filePath = null)
    {
        filePath ??= SettingsFilePath;
        try
        {
            string? directory = Path.GetDirectoryName(filePath);
            if (directory != null && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            string json = JsonSerializer.Serialize(_current, JsonOptions);
            File.WriteAllText(filePath, json);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Error saving settings");
        }
    }
}
