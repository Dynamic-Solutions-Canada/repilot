namespace Repilot.Models;

/// <summary>
/// A single entry in the curated Windows-function catalog. Each function is
/// executed either by synthesizing a <see cref="Combo"/> keystroke or by
/// shell-launching <see cref="ShellTarget"/> (an exe, ms-settings: URI, or
/// shell: location). Catalog entries are defined in code, not serialized;
/// only the <see cref="Id"/> is stored in settings.
/// </summary>
public sealed class WindowsFunction
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Group { get; init; }
    public required string Description { get; init; }

    /// <summary>Segoe Fluent Icons glyph shown next to the entry.</summary>
    public string Glyph { get; init; } = "";

    /// <summary>Keystroke to synthesize, when this function is a shortcut.</summary>
    public KeyCombo? Combo { get; init; }

    /// <summary>Target to shell-launch (exe path, ms-settings: URI, or shell: path).</summary>
    public string? ShellTarget { get; init; }

    /// <summary>Optional arguments passed when launching <see cref="ShellTarget"/>.</summary>
    public string? ShellArguments { get; init; }
}
