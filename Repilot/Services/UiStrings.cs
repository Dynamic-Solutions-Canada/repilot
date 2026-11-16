using Microsoft.Windows.ApplicationModel.Resources;
using System.Globalization;

namespace Repilot.Services;

/// <summary>
/// Reads localized settings-app text from <c>Strings/&lt;language&gt;/Resources.resw</c>.
/// XAML reaches the same resources through <c>x:Uid</c>; this class serves code-behind.
/// </summary>
/// <remarks>
/// Settings-app only. The key handler shows no text, so it never loads this class and
/// the keypress path stays free of resource lookups. The test projects link a stub with
/// the same API that reads the English <c>.resw</c> directly.
/// </remarks>
public static class UiStrings
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    // Created on first use, after App has applied any saved language override.
    private static readonly Lazy<ResourceLoader?> Loader = new(CreateLoader);

    /// <summary>
    /// Returns the localized string for <paramref name="key"/>. A missing resource returns
    /// <paramref name="fallback"/> when given, otherwise the key itself, so a gap is visible
    /// instead of blank.
    /// </summary>
    public static string Get(string key, string? fallback = null)
    {
        try
        {
            string? value = Loader.Value?.GetString(key);
            if (!string.IsNullOrEmpty(value))
            {
                return value;
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Missing string resource {Key}", key);
        }

        return fallback ?? key;
    }

    /// <summary>Formats a localized composite-format string such as <c>"Version {0}"</c>.</summary>
    public static string Format(string key, params object?[] args)
        => string.Format(CultureInfo.CurrentCulture, Get(key), args);

    private static ResourceLoader? CreateLoader()
    {
        try
        {
            return new ResourceLoader();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Could not open the app's string resources");
            return null;
        }
    }
}
