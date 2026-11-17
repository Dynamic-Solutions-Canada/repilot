// Portable harness for the settings file (CONF-3, CONF-4): the kit's real JsonSettingsStore with the
// app's real schema steps (SettingsMigrations), settings class and action model, run against real files
// in a temporary folder. Each scenario writes the file a real install could have, loads it the way
// SettingsManager does, and checks what the user would get back. Every file the store writes is also
// read with the key handler's own reader (KeySettings), because the handler reads the same settings.json.
// Exits non-zero on the first failure.
using System.Text.Json;
using System.Text.Json.Nodes;
using Repilot.Classes.Settings;
using Repilot.Models;
using Repilot.ViewModels;
using RepilotKey;
using WinUIAppKit.Settings;

// One 1.0.22 action per type, as that version's writer left them: values equal to their default
// (Type 0, a null Combo) were omitted, and empty strings were written.
var legacyActions = new (string Name, string Json)[]
{
    ("a shortcut", """{ "Type": 1, "Combo": { "Modifiers": 2, "VirtualKey": 9 }, "LaunchPath": "", "LaunchArguments": "", "LaunchDisplayName": "", "WindowsFunctionId": "", "Text": "" }"""),
    ("an app with arguments", """{ "Type": 2, "LaunchPath": "C:\\Tools\\app.exe", "LaunchArguments": "--fast \"x y\"", "LaunchDisplayName": "App", "WindowsFunctionId": "", "Text": "" }"""),
    ("a Windows function", """{ "Type": 3, "LaunchPath": "", "LaunchArguments": "", "LaunchDisplayName": "", "WindowsFunctionId": "task-view", "Text": "" }"""),
    ("text with a surrogate pair", """{ "Type": 4, "LaunchPath": "", "LaunchArguments": "", "LaunchDisplayName": "", "WindowsFunctionId": "", "Text": "ç Ç 😀 {0}" }"""),
    ("nothing", """{ "LaunchPath": "", "LaunchArguments": "", "LaunchDisplayName": "", "WindowsFunctionId": "", "Text": "" }"""),
};

var scenarios = new List<(string Name, Action<string> Body)>
{
    ("with no file, it is a first run, the file is written with a schema version, and the handler reads it", FirstRun),
};
foreach (var (name, json) in legacyActions)
{
    scenarios.Add(($"a 1.0.22 file with {name} keeps its action, theme, language and window, and the handler still reads it", path => LegacyFile(path, json)));
}
scenarios.AddRange(
[
    ("a 1.0.22 file that never saved a window size gets no window placement", NoWindowSize),
    ("a file that isn't JSON is kept as .bad, defaults are written, and it is not a first run", NotJson),
    ("a current file loads without a backup", CurrentVersion),
    ("a file from a newer version is backed up and loaded as far as this version understands", NewerVersion),
    ("Reset keeps the old file, and the action, theme and language are back to their defaults", ResetKeepsOldFile),
]);

string root = Path.Combine(Path.GetTempPath(), "repilot-settings-tests-" + Guid.NewGuid().ToString("N"));
try
{
    for (int i = 0; i < scenarios.Count; i++)
    {
        var (name, body) = scenarios[i];
        string folder = Path.Combine(root, i.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        try
        {
            body(Path.Combine(folder, "settings.json"));
        }
        catch (Exception ex)
        {
            Console.WriteLine("FAIL: " + name);
            Console.WriteLine("  " + ex.Message);
            return 1;
        }

        Console.WriteLine("PASS: " + name);
    }
}
finally
{
    Directory.Delete(root, recursive: true);
}

Console.WriteLine($"All {scenarios.Count} scenarios passed.");
return 0;

// ── Scenarios ───────────────────────────────────────────────────────────

void FirstRun(string path)
{
    var result = Store(path).Load();

    Check(result.Outcome == SettingsLoadOutcome.FirstRun, "the outcome is FirstRun");
    Check(File.Exists(path), "the file was written");
    Check(FileVersion(path) == SettingsMigrations.Steps.Count, "the file carries the current schema version");
    Check((int)result.Settings.AppTheme == 0 && result.Settings.LanguageOverride == "", "theme and language follow Windows");
    Check(result.Settings.Action.Type == CopilotActionType.None && result.Settings.SettingsWindow is null, "no action and no saved window");
    Check(HandlerReads(path).Type == CopilotActionType.None, "the handler reads the new file as no action");
}

void LegacyFile(string path, string actionJson)
{
    // As 1.0.22 wrote it: no schema version, the four window numbers, and the unused Startup and
    // LastKnownVersion values. AppTheme 2 and a language are set so their survival is visible.
    string original = $$"""
        {
          "AppTheme": 2,
          "Startup": true,
          "LanguageOverride": "fr-CA",
          "Action": {{actionJson}},
          "LastKnownVersion": "1.0.22",
          "SettingsWindowX": 120,
          "SettingsWindowY": 80,
          "SettingsWindowWidth": 1100,
          "SettingsWindowHeight": 760
        }
        """;
    File.WriteAllText(path, original);
    CopilotActionData before = KeySettings.Parse(original).Action;

    var result = Store(path).Load();
    var s = result.Settings;

    Check(result.Outcome == SettingsLoadOutcome.Migrated, "the outcome is Migrated, not a first run");
    Check(File.ReadAllText(path + ".v0.bak") == original, "the original file is kept as settings.json.v0.bak");
    Check(SameAction(s.Action, before), "the action loads unchanged");
    Check((int)s.AppTheme == 2 && s.LanguageOverride == "fr-CA", "theme and language are kept");
    Check(s.SettingsWindow is { X: 120, Y: 80, Width: 1100, Height: 760 }, "the window numbers become one placement");

    var saved = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    foreach (string gone in new[] { "Startup", "LastKnownVersion", "SettingsWindowX", "SettingsWindowY", "SettingsWindowWidth", "SettingsWindowHeight" })
    {
        Check(!saved.ContainsKey(gone), $"{gone} is no longer in the file");
    }

    Check(FileVersion(path) == SettingsMigrations.Steps.Count, "the file carries the current schema version");
    Check(SameAction(HandlerReads(path), before), "the key handler reads the same action from the upgraded file");

    var reloaded = Store(path).Load();
    Check(reloaded.Outcome == SettingsLoadOutcome.Loaded && SameAction(reloaded.Settings.Action, before), "a second launch loads it unchanged");
}

void NoWindowSize(string path)
{
    File.WriteAllText(path, """{ "AppTheme": 1, "SettingsWindowX": 5, "SettingsWindowY": 6 }""");
    var result = Store(path).Load();

    Check(result.Outcome == SettingsLoadOutcome.Migrated, "the outcome is Migrated");
    Check(result.Settings.SettingsWindow is null, "no placement, so the window opens at its default size");
    Check(!JsonNode.Parse(File.ReadAllText(path))!.AsObject().ContainsKey("SettingsWindowX"), "the stray numbers are gone");
}

void NotJson(string path)
{
    File.WriteAllText(path, "not json {");
    var result = Store(path).Load();

    Check(result.Outcome == SettingsLoadOutcome.Unreadable && result.NeedsResetNotice, "the outcome is Unreadable, so the app says so once");
    Check(File.ReadAllText(path + ".bad") == "not json {", "the unreadable file is kept as .bad");
    Check(FileVersion(path) == SettingsMigrations.Steps.Count, "defaults were written with the current schema version");
    Check(HandlerReads(path).Type == CopilotActionType.None, "the handler reads the defaults as no action");
}

void CurrentVersion(string path)
{
    var store = Store(path);
    store.Load();
    var result = store.Load();

    Check(result.Outcome == SettingsLoadOutcome.Loaded, "the outcome is Loaded");
    Check(result.BackupPath == null && Directory.GetFiles(Path.GetDirectoryName(path)!).Length == 1, "no backup was made");
}

void NewerVersion(string path)
{
    string newer = """{ "SchemaVersion": 99, "AppTheme": 1, "Action": { "Type": 3, "WindowsFunctionId": "lock" }, "SomethingNew": true }""";
    File.WriteAllText(path, newer);
    var result = Store(path).Load();

    Check(result.Outcome == SettingsLoadOutcome.NewerVersion, "the outcome is NewerVersion");
    Check(File.ReadAllText(path + ".v99.bak") == newer, "the newer file is kept as .v99.bak");
    Check((int)result.Settings.AppTheme == 1 && result.Settings.Action.WindowsFunctionId == "lock", "what this version understands is loaded");
    Check(HandlerReads(path).WindowsFunctionId == "lock", "the handler still reads the action");
}

void ResetKeepsOldFile(string path)
{
    var store = Store(path);
    File.WriteAllText(path, """{ "AppTheme": 2, "LanguageOverride": "de-DE", "Action": { "Type": 3, "WindowsFunctionId": "task-view" } }""");
    store.Load();

    var result = store.Reset();
    var resetFiles = Directory.GetFiles(Path.GetDirectoryName(path)!, "settings.json.*.reset");

    Check(result.Outcome == SettingsLoadOutcome.Reset, "the outcome is Reset");
    Check(resetFiles.Length == 1 && File.ReadAllText(resetFiles[0]).Contains("de-DE"), "the previous file is kept as .reset");
    Check((int)result.Settings.AppTheme == 0 && result.Settings.LanguageOverride == "", "theme and language follow Windows again");
    Check(result.Settings.Action.Type == CopilotActionType.None, "the key's action is back to nothing");
    Check(HandlerReads(path).Type == CopilotActionType.None, "the handler reads the reset file as no action");
    Check(FileVersion(path) == SettingsMigrations.Steps.Count, "the new file carries the current schema version");
}

// ── Helpers ─────────────────────────────────────────────────────────────

// The store as SettingsManager creates it, at a test path.
static JsonSettingsStore<UserSettings> Store(string path) => new(path, SettingsMigrations.Steps);

// What the key handler would run for this file, read with its own source-generated reader.
static CopilotActionData HandlerReads(string path) => KeySettings.Parse(File.ReadAllText(path)).Action;

// Two actions are the same when they serialize the same way.
static bool SameAction(CopilotActionData a, CopilotActionData b) =>
    JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);

static int? FileVersion(string path) =>
    JsonNode.Parse(File.ReadAllText(path))?[JsonSettingsStore<UserSettings>.SchemaVersionProperty]?.GetValue<int>();

static void Check(bool condition, string what)
{
    if (!condition)
    {
        throw new Exception(what);
    }
}
