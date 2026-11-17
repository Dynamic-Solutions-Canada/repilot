using System.Text.Json;
using System.Text.Json.Serialization;
using Repilot.Models;

namespace RepilotKey;

/// <summary>
/// The subset of settings.json the handler needs; every other field is ignored, including the settings
/// app's <c>SchemaVersion</c>. This reader is the contract the settings app's store must keep: the
/// settings harness (tests/Repilot.Settings.Tests) links this file and parses the store's output with it.
/// Source-generated, so it stays trim and AOT safe.
/// </summary>
internal sealed class KeySettings
{
    public CopilotActionData Action { get; set; } = new();

    /// <summary>Reads settings.json text; returns defaults when it holds JSON null.</summary>
    public static KeySettings Parse(string json) =>
        JsonSerializer.Deserialize(json, KeyJsonContext.Default.KeySettings) ?? new KeySettings();
}

[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(KeySettings))]
internal partial class KeyJsonContext : JsonSerializerContext { }
