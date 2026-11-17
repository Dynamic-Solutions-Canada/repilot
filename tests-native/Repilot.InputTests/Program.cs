using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Forms;
using Repilot.Models;
using Repilot.Services;

/// <summary>Native Windows regression checks using a disposable, focused text field.</summary>
internal static class Program
{
    /// <summary>The variable that must be set to "1" before this suite will send any input (TEST-10).</summary>
    private const string OptInVariable = "REPILOT_INPUT_TESTS";

    /// <summary>
    /// Runs the checks only when explicitly asked: they type into a window and switch this thread's
    /// keyboard layout, which would disturb whoever is using the machine. Returns 2 when refused, so an
    /// accidental run is never reported as a pass.
    /// </summary>
    [STAThread]
    private static int Main()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable(OptInVariable) != "1")
        {
            Console.Error.WriteLine(
                $"REFUSED: this suite sends real keystrokes. On Windows, set {OptInVariable}=1 on a machine nobody is using " +
                "(see README.md), keep its window focused, then run it again.");
            return 2;
        }

        try
        {
            CheckSettings();
            CheckLayoutNames();
            CheckTextInput();
            Console.WriteLine("PASS: settings compatibility, Portuguese key labels, Unicode input, shortcut input.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine(error);
            return 1;
        }
    }

    /// <summary>Exercise source-generated JSON like the trimmed handler, including legacy settings.</summary>
    private static void CheckSettings()
    {
        var action = new CopilotActionData { Type = CopilotActionType.Text, Text = "ç Ç 😀 " };
        string json = JsonSerializer.Serialize(action, InputJsonContext.Default.CopilotActionData);
        var restored = JsonSerializer.Deserialize(json, InputJsonContext.Default.CopilotActionData)!;
        Require(restored.Type == CopilotActionType.Text && restored.Text == action.Text, "Text settings round trip");
        Require(ActionSummary.Describe(restored) == "Type text: " + action.Text, "Text summary");

        var legacy = JsonSerializer.Deserialize("{\"Type\":1,\"Combo\":{\"Modifiers\":2,\"VirtualKey\":9}}",
            InputJsonContext.Default.CopilotActionData)!;
        Require(legacy.Text == "" && legacy.Combo?.ToString() == "Alt + Tab", "Legacy shortcut settings");
        Require(ActionSummary.Describe(new CopilotActionData { Type = CopilotActionType.Text })
            == "Type text (not set)", "Empty text summary");
    }

    /// <summary>Change only this thread's layout and restore it, leaving user settings alone.</summary>
    private static void CheckLayoutNames()
    {
        IntPtr original = GetKeyboardLayout(0);
        var existing = new IntPtr[GetKeyboardLayoutList(0, null)];
        GetKeyboardLayoutList(existing.Length, existing);
        foreach (string layoutId in new[] { "00000409", "00000416", "00000816" })
        {
            IntPtr layout = LoadKeyboardLayoutW(layoutId, 0);
            Require(layout != IntPtr.Zero, "Load layout " + layoutId);
            try
            {
                Require(ActivateKeyboardLayout(layout, 0) != IntPtr.Zero, "Activate test thread layout");
                // Brazil uses OEM_1 for cedilla; Portugal uses OEM_3.
                int virtualKey = layoutId == "00000816" ? 0xC0 : 0xBA;
                Require(KeyNames.Name(virtualKey).Equals(layoutId == "00000409" ? ";" : "ç",
                    StringComparison.OrdinalIgnoreCase), "Layout-specific OEM key " + layoutId);
                Require(KeyNames.Name(0x61) == "Num 1" && KeyNames.Name(0x70) == "F1", "Stable special key names");
            }
            finally
            {
                ActivateKeyboardLayout(original, 0);
                if (!existing.Contains(layout)) UnloadKeyboardLayout(layout);
            }
        }
    }

    /// <summary>
    /// Confirm real WM_CHAR delivery into a standard text field, including surrogate
    /// pairs and whitespace. Refuse to inject if the test window does not have focus.
    /// </summary>
    private static void CheckTextInput()
    {
        using var form = new Form { Text = "Repilot input regression test", Width = 460, Height = 140 };
        using var field = new TextBox { Dock = DockStyle.Fill };
        form.Controls.Add(field);
        using var timer = new System.Windows.Forms.Timer { Interval = 250 };
        Exception? failure = null;
        int step = 0;
        const string text = "ç Ç á € 😀 ";
        timer.Tick += (_, _) =>
        {
            try
            {
                Require(GetForegroundWindow() == form.Handle && field.Focused, "Test window must have keyboard focus");
                switch (step++)
                {
                    case 0:
                        ActionExecutor.RunSync(new CopilotActionData { Type = CopilotActionType.Text, Text = text });
                        break;
                    case 1:
                        Require(field.Text == text, "Unicode input: " + field.Text);
                        ActionExecutor.RunSync(new CopilotActionData { Type = CopilotActionType.Text });
                        ActionExecutor.RunSync(new CopilotActionData { Type = CopilotActionType.None });
                        break;
                    case 2:
                        Require(field.Text == text, "Empty and None actions must not type");
                        ActionExecutor.RunSync(new CopilotActionData
                        {
                            Type = CopilotActionType.KeyCombo,
                            Combo = new KeyCombo(KeyMods.None, 0x41),
                        });
                        break;
                    default:
                        Require(field.Text.Length == text.Length + 1, "Existing shortcut still types one key");
                        form.Close();
                        break;
                }
            }
            catch (Exception error)
            {
                failure = error;
                form.Close();
            }
        };
        form.Shown += (_, _) =>
        {
            form.Activate();
            field.Focus();
            timer.Start();
        };
        Application.Run(form);
        if (failure != null) throw failure;
        Require(step == 4, "Input test completed");
    }

    /// <summary>Fail immediately when a regression contract is not met.</summary>
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint threadId);
    [DllImport("user32.dll")]
    private static extern int GetKeyboardLayoutList(int count, [Out] IntPtr[]? layouts);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern IntPtr LoadKeyboardLayoutW(string layoutId, uint flags);
    [DllImport("user32.dll")]
    private static extern IntPtr ActivateKeyboardLayout(IntPtr layout, uint flags);
    [DllImport("user32.dll")]
    private static extern bool UnloadKeyboardLayout(IntPtr layout);
}

[JsonSerializable(typeof(CopilotActionData))]
internal partial class InputJsonContext : JsonSerializerContext { }
