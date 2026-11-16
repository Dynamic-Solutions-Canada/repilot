using System.Globalization;
using System.Xml.Linq;

namespace Repilot.Services;

/// <summary>
/// Test stand-in for the settings app's <c>UiStrings</c>, with the same API. It reads the
/// English <c>Resources.resw</c> copied next to the test binary, so linked UI code such as
/// <c>ActionSummary</c> runs without the Windows App SDK resource system.
/// </summary>
public static class UiStrings
{
    private static readonly Dictionary<string, string> Values = Load();

    public static string Get(string key, string? fallback = null)
        => Values.TryGetValue(key, out string? value) ? value : fallback ?? key;

    public static string Format(string key, params object?[] args)
        => string.Format(CultureInfo.InvariantCulture, Get(key), args);

    private static Dictionary<string, string> Load()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Resources.resw");
        return XDocument.Load(path).Root!
            .Elements("data")
            .ToDictionary(data => (string)data.Attribute("name")!, data => (string?)data.Element("value") ?? "");
    }
}
