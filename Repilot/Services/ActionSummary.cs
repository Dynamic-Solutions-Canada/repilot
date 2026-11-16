using Repilot.Models;

namespace Repilot.Services;

/// <summary>Human-readable one-line description of a configured action (for the settings UI).</summary>
public static class ActionSummary
{
    /// <summary>
    /// Describes <paramref name="a"/> in the current app language. User-entered text, app
    /// names and key names are inserted verbatim.
    /// </summary>
    public static string Describe(CopilotActionData a) => a.Type switch
    {
        CopilotActionType.None => UiStrings.Get("SummaryNone"),
        CopilotActionType.Text => string.IsNullOrEmpty(a.Text)
            ? UiStrings.Get("SummaryTextUnset")
            : UiStrings.Format("SummaryText", a.Text),
        CopilotActionType.KeyCombo => a.Combo is { IsEmpty: false }
            ? UiStrings.Format("SummaryKeys", a.Combo)
            : UiStrings.Get("SummaryKeysUnset"),
        CopilotActionType.LaunchApp => string.IsNullOrWhiteSpace(a.LaunchPath)
            ? UiStrings.Get("SummaryAppUnset")
            : UiStrings.Format("SummaryOpen", string.IsNullOrWhiteSpace(a.LaunchDisplayName) ? a.LaunchPath : a.LaunchDisplayName),
        CopilotActionType.WindowsFunction => string.IsNullOrWhiteSpace(a.WindowsFunctionId)
            ? UiStrings.Get("SummaryFunctionUnset")
            : UiStrings.Format("SummaryFunction", WindowsFunctionText.NameFor(a.WindowsFunctionId)),
        _ => UiStrings.Get("Unknown"),
    };
}
