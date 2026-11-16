using Repilot.Models;
using Repilot.Services;

namespace Repilot.ViewModels;

/// <summary>
/// One row of the Windows-function picker: a catalog entry with its text resolved in the
/// current app language. Selection still uses <see cref="Function"/>'s Id.
/// </summary>
public sealed class WindowsFunctionItem
{
    public WindowsFunctionItem(WindowsFunction function)
    {
        Function = function;
        Name = WindowsFunctionText.Name(function);
        Description = WindowsFunctionText.Description(function);
        Group = WindowsFunctionText.Group(function);
    }

    public WindowsFunction Function { get; }

    public string Name { get; }

    public string Description { get; }

    public string Group { get; }

    /// <summary>
    /// True when <paramref name="query"/> appears in the localized name, description or group,
    /// or in the English name, so familiar English terms still find a function in any language.
    /// </summary>
    public bool Matches(string query)
        => Name.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || Description.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || Group.Contains(query, StringComparison.CurrentCultureIgnoreCase)
        || Function.Name.Contains(query, StringComparison.OrdinalIgnoreCase);
}
