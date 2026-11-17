using System.Text.Json.Nodes;

namespace Repilot.Classes.Settings;

/// <summary>
/// Upgrades for older settings files, one step per schema version (CONF-3). The kit's versioned
/// <see cref="WinUIAppKit.Settings.JsonSettingsStore{T}"/> backs the file up first, then runs the step at
/// index <c>n</c> to take it from version <c>n</c> to <c>n + 1</c>. A file with no
/// <c>SchemaVersion</c> is version 0. Steps are never removed or reordered once shipped.
/// </summary>
internal static class SettingsMigrations
{
    /// <summary>The steps, in order; the current schema version is their count.</summary>
    public static IReadOnlyList<Action<JsonObject>> Steps { get; } =
    [
        FromUnversioned,
    ];

    /// <summary>
    /// 0 to 1, the files Repilot 1.0.22 and earlier wrote. The four window numbers become one
    /// <c>SettingsWindow</c> placement (kept only when a size was saved, as the old code required), and
    /// the unused <c>Startup</c> and <c>LastKnownVersion</c> values are dropped. <c>Action</c>,
    /// <c>AppTheme</c> and <c>LanguageOverride</c> keep their names and values.
    /// </summary>
    private static void FromUnversioned(JsonObject root)
    {
        int? x = TakeInt(root, "SettingsWindowX");
        int? y = TakeInt(root, "SettingsWindowY");
        int? width = TakeInt(root, "SettingsWindowWidth");
        int? height = TakeInt(root, "SettingsWindowHeight");

        if (width > 0 && height > 0)
        {
            root["SettingsWindow"] = new JsonObject
            {
                ["X"] = x ?? 0,
                ["Y"] = y ?? 0,
                ["Width"] = width,
                ["Height"] = height,
            };
        }

        root.Remove("Startup");
        root.Remove("LastKnownVersion");
    }

    /// <summary>Removes <paramref name="name"/> and returns it when it was a number; null otherwise.</summary>
    private static int? TakeInt(JsonObject root, string name)
    {
        if (!root.Remove(name, out JsonNode? node) || node is not JsonValue value)
        {
            return null;
        }

        return value.TryGetValue(out int number) ? number : null;
    }
}
