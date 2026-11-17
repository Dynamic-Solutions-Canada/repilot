using System.Text.RegularExpressions;
using System.Xml.Linq;
using Repilot.Models;
using Repilot.Services;

/// <summary>
/// Checks the settings app's string resources against the source that uses them: every
/// language complete and consistent, every XAML x:Uid and code key present, the function
/// catalog's English matching its resources, and the package languages matching the picker.
/// These are source checks; they do not prove Windows resolves the strings at runtime.
/// </summary>
internal static class Program
{
    private static readonly string Root = FindRepositoryRoot();
    private static readonly List<string> Failures = [];

    private static int Main()
    {
        var catalogs = LoadCatalogs();
        Dictionary<string, string> english = catalogs["en-US"];

        CheckLanguagesMatch(catalogs);
        CheckXamlUids(english);
        CheckCodeKeys(english);
        CheckFunctionCatalog(english);
        CheckPackageLanguages();
        CheckEnglishSummaries();

        if (Failures.Count > 0)
        {
            Failures.ForEach(failure => Console.Error.WriteLine("FAIL: " + failure));
            return 1;
        }

        Console.WriteLine($"PASS: {english.Count} strings in {catalogs.Count} languages; XAML, code, catalog and package languages consistent.");
        return 0;
    }

    /// <summary>Loads every Strings/&lt;lang&gt;/Resources.resw, rejecting duplicate names.</summary>
    private static Dictionary<string, Dictionary<string, string>> LoadCatalogs()
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        foreach (string dir in Directory.GetDirectories(Path.Combine(Root, "Repilot", "Strings")))
        {
            string language = Path.GetFileName(dir);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XElement data in XDocument.Load(Path.Combine(dir, "Resources.resw")).Root!.Elements("data"))
            {
                string name = (string)data.Attribute("name")!;
                if (!values.TryAdd(name, (string?)data.Element("value") ?? ""))
                {
                    Fail($"{language}: duplicate resource {name}");
                }
            }

            result[language] = values;
        }

        return result;
    }

    /// <summary>Same names everywhere, nothing empty, same placeholders, no em dashes.</summary>
    private static void CheckLanguagesMatch(Dictionary<string, Dictionary<string, string>> catalogs)
    {
        Dictionary<string, string> english = catalogs["en-US"];
        foreach (var (language, values) in catalogs)
        {
            foreach (string missing in english.Keys.Except(values.Keys))
            {
                Fail($"{language}: missing {missing}");
            }

            foreach (string extra in values.Keys.Except(english.Keys))
            {
                Fail($"{language}: unexpected {extra}");
            }

            foreach (var (name, value) in values)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    Fail($"{language}: empty {name}");
                }

                if (value.Contains('—'))
                {
                    Fail($"{language}: em dash in {name}");
                }

                if (english.TryGetValue(name, out string? source) && Placeholders(source) != Placeholders(value))
                {
                    Fail($"{language}: placeholders differ in {name}");
                }
            }
        }

        // Windows turns "A.B" into the path A/B, so a plain key named A would collide with it.
        var uidPrefixes = english.Keys.Where(k => k.Contains('.')).Select(k => k.Split('.')[0]).ToHashSet();
        foreach (string clash in english.Keys.Where(k => !k.Contains('.') && uidPrefixes.Contains(k)))
        {
            Fail($"{clash} is both a code key and an x:Uid prefix");
        }
    }

    /// <summary>
    /// Properties each element type accepts from resources. WinUI applies every Uid.Property
    /// resource to the element and fails to load the page if the property does not exist.
    /// </summary>
    /// <summary>The attached property an icon-plus-label button sets for its accessible name.</summary>
    private const string AutomationName = "[using:Microsoft.UI.Xaml.Automation]AutomationProperties.Name";

    private static readonly Dictionary<string, string[]> UidProperties = new()
    {
        ["TextBlock"] = ["Text"],
        ["Button"] = ["Content", AutomationName],
        ["ComboBox"] = ["Header"],
        ["HyperlinkButton"] = ["Content"],
        ["ComboBoxItem"] = ["Content"],
        ["NavigationViewItem"] = ["Content"],
        ["SelectorBarItem"] = ["Text"],
        ["TextBox"] = ["Header", "PlaceholderText"],
        ["AutoSuggestBox"] = ["PlaceholderText"],
        ["Expander"] = ["Header"],
        ["InfoBar"] = ["Title", "Message"],
    };

    /// <summary>
    /// Every x:Uid sits on one element, has resources, and each of its resources names a
    /// property that element type has; every Uid.Property resource has an x:Uid.
    /// </summary>
    private static void CheckXamlUids(Dictionary<string, string> english)
    {
        var uses = Directory.GetFiles(Path.Combine(Root, "Repilot"), "*.xaml", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), "<([A-Za-z]+)\\s[^>]*?x:Uid=\"([^\"]+)\"")
                .Select(m => (Element: m.Groups[1].Value, Uid: m.Groups[2].Value)))
            .ToList();

        foreach (var group in uses.GroupBy(use => use.Uid).Where(g => g.Count() > 1))
        {
            Fail($"x:Uid {group.Key} is used on {group.Count()} elements; give each its own");
        }

        foreach (var (element, uid) in uses)
        {
            string[] properties = english.Keys
                .Where(k => k.StartsWith(uid + ".", StringComparison.Ordinal))
                .Select(k => k[(uid.Length + 1)..])
                .ToArray();
            if (properties.Length == 0)
            {
                Fail($"x:Uid {uid} has no resources");
            }

            if (!UidProperties.TryGetValue(element, out string[]? allowed))
            {
                Fail($"x:Uid {uid} is on {element}; add its properties to the test's list");
            }
            else
            {
                foreach (string property in properties.Except(allowed))
                {
                    Fail($"x:Uid {uid} sets {property}, which {element} does not have");
                }
            }
        }

        var uids = uses.Select(use => use.Uid).ToHashSet();
        foreach (string name in english.Keys.Where(k => k.Contains('.') && !uids.Contains(k.Split('.')[0])))
        {
            Fail($"resource {name} is not used by any x:Uid");
        }
    }

    /// <summary>
    /// Literal keys passed to UiStrings exist, and every plain
    /// key is referenced somewhere (catalog keys are covered by the catalog check).
    /// </summary>
    private static void CheckCodeKeys(Dictionary<string, string> english)
    {
        string code = string.Join('\n', Directory.GetFiles(Path.Combine(Root, "Repilot"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !IsBuildOutput(path))
            .Select(File.ReadAllText));

        foreach (Match call in Regex.Matches(code, "UiStrings\\.(?:Get|Format)\\(\"([^\"]+)\""))
        {
            if (!english.ContainsKey(call.Groups[1].Value))
            {
                Fail($"code uses missing key {call.Groups[1].Value}");
            }
        }

        foreach (string name in english.Keys.Where(k => !k.Contains('.') && !k.StartsWith("Function", StringComparison.Ordinal)))
        {
            if (!code.Contains($"\"{name}\"", StringComparison.Ordinal))
            {
                Fail($"resource {name} is never used");
            }
        }
    }

    /// <summary>The en-US resources match the catalog's English, and no catalog resource is orphaned.</summary>
    private static void CheckFunctionCatalog(Dictionary<string, string> english)
    {
        var expected = new HashSet<string>(StringComparer.Ordinal);
        foreach (WindowsFunction function in WindowsFunctionCatalog.All)
        {
            Expect(english, WindowsFunctionText.NameKey(function.Id), function.Name, expected);
            Expect(english, WindowsFunctionText.DescriptionKey(function.Id), function.Description, expected);
            Expect(english, WindowsFunctionText.GroupKey(function.Group), function.Group, expected);
        }

        foreach (string orphan in english.Keys.Where(k => k.StartsWith("Function", StringComparison.Ordinal) && !expected.Contains(k)))
        {
            Fail($"catalog resource {orphan} has no catalog entry");
        }
    }

    private static void Expect(Dictionary<string, string> english, string key, string value, HashSet<string> seen)
    {
        seen.Add(key);
        if (!english.TryGetValue(key, out string? actual))
        {
            Fail($"catalog resource {key} is missing");
        }
        else if (actual != value)
        {
            Fail($"{key} is \"{actual}\" but the catalog says \"{value}\"");
        }
    }

    /// <summary>The picker, the manifest and the Strings folders describe the same nine languages.</summary>
    private static void CheckPackageLanguages()
    {
        string[] picker = Regex.Matches(File.ReadAllText(Path.Combine(Root, "Repilot", "Classes", "AppLanguage.cs")), "\\(\"([a-z]{2}-[A-Z]{2})\", \"[a-z]{2}-[A-Z]{2}\", \"")
            .Select(m => m.Groups[1].Value).ToArray();
        string[] manifest = XDocument.Load(Path.Combine(Root, "RepilotMSIX", "Package.appxmanifest")).Descendants()
            .Where(e => e.Name.LocalName == "Resource")
            .Select(e => (string)e.Attribute("Language")!).ToArray();
        string[] expected = ["en-US", "en-CA", "es-ES", "fr-FR", "fr-CA", "de-DE", "zh-CN", "pt-BR", "ja-JP"];

        if (!picker.SequenceEqual(expected))
        {
            Fail($"picker languages are {string.Join(',', picker)}");
        }

        if (!manifest.SequenceEqual(expected))
        {
            Fail($"manifest languages are {string.Join(',', manifest)}");
        }

        // Canadian English and French are packaged from the US English and France French files.
        var aliases = new Dictionary<string, string> { ["en-CA"] = "en-US", ["fr-CA"] = "fr-FR" };
        foreach (string language in expected)
        {
            string folder = aliases.GetValueOrDefault(language, language);
            if (!File.Exists(Path.Combine(Root, "Repilot", "Strings", folder, "Resources.resw")))
            {
                Fail($"no strings for {language} (expected Strings/{folder})");
            }
        }
    }

    /// <summary>Linked UI code renders English from the resources, with user text inserted verbatim.</summary>
    private static void CheckEnglishSummaries()
    {
        Require(ActionSummary.Describe(new CopilotActionData()) == "Do nothing", "None summary");
        Require(ActionSummary.Describe(new CopilotActionData { Type = CopilotActionType.Text, Text = "{0} ç" }) == "Type text: {0} ç", "Text summary keeps braces");
        Require(ActionSummary.Describe(new CopilotActionData { Type = CopilotActionType.WindowsFunction, WindowsFunctionId = "task-view" }) == "Windows function: Task View", "Function summary");
        Require(ActionSummary.Describe(new CopilotActionData { Type = CopilotActionType.WindowsFunction, WindowsFunctionId = "retired-id" }) == "Windows function: retired-id", "Unknown function Id");
    }

    private static string Placeholders(string value)
        => string.Join(',', Regex.Matches(value, "\\{\\d+\\}").Select(m => m.Value).Order());

    private static bool IsBuildOutput(string path)
        => path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
        || path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}");

    private static void Require(bool condition, string name)
    {
        if (!condition)
        {
            Fail(name);
        }
    }

    private static void Fail(string message) => Failures.Add(message);

    private static string FindRepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Repilot.slnx")))
            {
                return dir.FullName;
            }
        }

        throw new InvalidOperationException("Run from inside the repository (Repilot.slnx not found).");
    }
}
