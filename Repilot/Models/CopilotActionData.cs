namespace Repilot.Models;

/// <summary>What happens for a Copilot-key gesture.</summary>
public enum CopilotActionType
{
    /// <summary>Do nothing.</summary>
    None = 0,
    /// <summary>Synthesize a custom keyboard chord via SendInput.</summary>
    KeyCombo = 1,
    /// <summary>Launch an app, file, or URI.</summary>
    LaunchApp = 2,
    /// <summary>Run a curated Windows function (looked up in the catalog by Id).</summary>
    WindowsFunction = 3,
    /// <summary>Type Unicode text into the foreground application.</summary>
    Text = 4,
}

/// <summary>
/// Plain serializable action data shared by the WinUI settings app and the
/// Native-AOT key handler. (No INotifyPropertyChanged: kept POCO so it works
/// under AOT and round-trips identically through System.Text.Json in both.)
/// </summary>
public sealed class CopilotActionData
{
    public CopilotActionType Type { get; set; }
    public KeyCombo? Combo { get; set; }
    public string LaunchPath { get; set; } = "";
    public string LaunchArguments { get; set; } = "";
    public string LaunchDisplayName { get; set; } = "";
    public string WindowsFunctionId { get; set; } = "";
    public string Text { get; set; } = "";
}
