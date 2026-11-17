using Repilot.Classes.Settings;
using Microsoft.Windows.Globalization;
using WinUIAppKit.Localization;

namespace Repilot.Classes;

/// <summary>
/// The settings app's display language: follow Windows (the default) or a saved choice
/// from <see cref="Supported"/>. Only the language of Repilot's own text changes; number
/// and date formats keep following the Windows regional settings.
/// </summary>
/// <remarks>
/// The choice is stored in settings.json and applied once at startup through the Windows
/// App SDK language override, before any window or resource lookup. WinUI does not
/// re-resolve already-loaded strings, so a change takes effect when the settings app is
/// next opened. The key handler never reads this setting.
/// </remarks>
internal static class AppLanguage
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>
    /// The nine-locale set (LOC-1), in picker order, as the kit's <see cref="SupportedLanguages"/> so the
    /// kit's shared labels follow the same choice. Canadian English and French reuse the US English and
    /// France French text; Windows resource matching maps them to those files, while keeping their own
    /// identities for selection. Keep this list, the manifest's languages and the Strings folders in step.
    /// </summary>
    internal static SupportedLanguages Supported { get; } = new("en-US",
    [
        ("en-US", "en-US", "English (United States)"),
        ("en-CA", "en-US", "English (Canada)"),
        ("es-ES", "es-ES", "Español (España)"),
        ("fr-FR", "fr-FR", "Français (France)"),
        ("fr-CA", "fr-FR", "Français (Canada)"),
        ("de-DE", "de-DE", "Deutsch (Deutschland)"),
        ("zh-CN", "zh-CN", "简体中文（中国）"),
        ("pt-BR", "pt-BR", "Português (Brasil)"),
        ("ja-JP", "ja-JP", "日本語（日本）"),
    ]);

    /// <summary>The override applied when this process started ("" = follow Windows).</summary>
    internal static string AppliedAtStartup { get; private set; } = "";

    /// <summary>
    /// Applies the saved choice to this process. Call once, before any XAML or string
    /// resource is loaded. An unknown saved value (hand-edited or from a future version)
    /// is treated as "follow Windows" without rewriting the file.
    /// </summary>
    internal static void ApplySaved()
    {
        string tag = Normalize(SettingsManager.Current.LanguageOverride);

        try
        {
            // Always assign, including "", so a stale override left by an earlier run is cleared.
            ApplicationLanguages.PrimaryLanguageOverride = tag;
            AppliedAtStartup = tag;
        }
        catch (Exception ex)
        {
            // Never block startup over a language preference; Windows' language still applies.
            Logger.Warn(ex, "Could not apply app language override {Tag}", tag);
        }
    }

    /// <summary>
    /// Saves a new choice ("" = follow Windows) for the next launch.
    /// Returns true when it differs from the language this process is showing.
    /// </summary>
    internal static bool SaveChoice(string tag)
    {
        string normalized = Normalize(tag);
        SettingsManager.Current.LanguageOverride = normalized;
        SettingsManager.SaveSettings();
        return normalized != AppliedAtStartup;
    }

    /// <summary>Returns the supported tag matching <paramref name="tag"/> (case-insensitive), or "".</summary>
    internal static string Normalize(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return "";
        }

        string? match = Supported.Identities.FirstOrDefault(identity =>
            identity.Equals(tag.Trim(), StringComparison.OrdinalIgnoreCase));
        return match ?? "";
    }
}
