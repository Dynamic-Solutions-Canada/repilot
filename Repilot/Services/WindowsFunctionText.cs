using Repilot.Models;

namespace Repilot.Services;

/// <summary>
/// Localized display text for <see cref="WindowsFunctionCatalog"/> entries, looked up by
/// the entry's stable <see cref="WindowsFunction.Id"/>.
/// </summary>
/// <remarks>
/// The catalog itself stays English and WinUI-free because it is linked into the key
/// handler, which only needs Ids, shortcuts and targets. Settings store the Id, never this
/// text, so changing language cannot change what a saved action does. The catalog's English
/// is the fallback, and the localization tests require the en-US resources to match it.
/// </remarks>
public static class WindowsFunctionText
{
    /// <summary>Localized function name, e.g. "Taskansicht" for task-view in German.</summary>
    public static string Name(WindowsFunction function)
        => UiStrings.Get(NameKey(function.Id), function.Name);

    /// <summary>Localized one-line description of what the function does.</summary>
    public static string Description(WindowsFunction function)
        => UiStrings.Get(DescriptionKey(function.Id), function.Description);

    /// <summary>Localized heading of the function's group.</summary>
    public static string Group(WindowsFunction function)
        => UiStrings.Get(GroupKey(function.Group), function.Group);

    /// <summary>Localized name for a saved function Id, or the Id itself when it is no longer in the catalog.</summary>
    public static string NameFor(string id)
        => WindowsFunctionCatalog.TryGet(id) is { } function ? Name(function) : id;

    /// <summary>Resource key of an entry's name.</summary>
    public static string NameKey(string id) => $"Function_{id}_Name";

    /// <summary>Resource key of an entry's description.</summary>
    public static string DescriptionKey(string id) => $"Function_{id}_Description";

    /// <summary>
    /// Resource key of a group, derived from its English heading with spaces and
    /// punctuation removed ("Capture &amp; recording" becomes FunctionGroup_Capturerecording).
    /// </summary>
    public static string GroupKey(string englishGroup)
        => "FunctionGroup_" + new string(englishGroup.Where(char.IsLetterOrDigit).ToArray());
}
