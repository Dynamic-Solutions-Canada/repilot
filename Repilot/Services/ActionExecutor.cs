using Repilot.Models;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Repilot.Services;

/// <summary>
/// Executes a <see cref="CopilotActionData"/>: synthesizes keystrokes via SendInput
/// or launches an app / URI. Self-contained P/Invoke so it can be linked into the
/// Native-AOT key handler without any WinUI/windowing dependencies.
/// </summary>
public static class ActionExecutor
{
    private const ushort VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_APPS = 0x5D, VK_SHIFT = 0x10,
        VK_CONTROL = 0x11, VK_MENU = 0x12;

    /// <summary>Optional sink for errors from <see cref="Run"/> (the WinUI app wires NLog here).</summary>
    public static Action<Exception>? ErrorSink { get; set; }

    /// <summary>Fire-and-forget (used by the settings app's Test button so the UI never blocks).</summary>
    public static void Run(CopilotActionData action) => _ = Task.Run(() =>
    {
        try { RunSync(action); }
        catch (Exception ex) { ErrorSink?.Invoke(ex); }
    });

    /// <summary>
    /// Runs the action on the calling thread (used by the key handler, which then
    /// exits). May throw — callers that need it should catch.
    /// </summary>
    public static void RunSync(CopilotActionData action)
    {
        switch (action.Type)
        {
            case CopilotActionType.None:
                break;
            case CopilotActionType.Text:
                if (!string.IsNullOrEmpty(action.Text))
                {
                    SendText(action.Text);
                }

                break;
            case CopilotActionType.KeyCombo:
                if (action.Combo is { IsEmpty: false } combo)
                {
                    SendCombo(combo);
                }

                break;
            case CopilotActionType.LaunchApp:
                if (!string.IsNullOrWhiteSpace(action.LaunchPath))
                {
                    Launch(action.LaunchPath, action.LaunchArguments);
                }

                break;
            case CopilotActionType.WindowsFunction:
                RunWindowsFunction(action.WindowsFunctionId);
                break;
        }
    }

    private static void RunWindowsFunction(string id)
    {
        var fn = WindowsFunctionCatalog.TryGet(id);
        if (fn == null)
        {
            return;
        }

        if (fn.Combo is { IsEmpty: false } combo)
        {
            SendCombo(combo);
        }
        else if (!string.IsNullOrWhiteSpace(fn.ShellTarget))
        {
            Launch(fn.ShellTarget!, fn.ShellArguments ?? "");
        }
    }

    private static void Launch(string target, string arguments)
    {
        // shell:AppsFolder\{AUMID} (and other shell: locations) are launched through
        // explorer.exe, which resolves them reliably. Everything else (exe paths, URIs
        // like https:// or ms-settings:, documents) goes through ShellExecute.
        if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
        {
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = target, UseShellExecute = true });
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = target,
            Arguments = arguments ?? "",
            UseShellExecute = true,
        });
    }

    // ── SendInput synthesis ──────────────────────────────────────────

    /// <summary>
    /// Sends literal text to the foreground app without translating through a keyboard
    /// layout or using the clipboard. Each UTF-16 code unit gets a down/up pair, so
    /// supplementary characters are sent as surrogate pairs. Requires a text-input
    /// target that accepts Unicode keyboard packets; elevated targets may block input.
    /// </summary>
    private static void SendText(string text)
    {
        var inputs = new List<INPUT>();
        foreach (char character in text)
        {
            AddUnicodeKey(inputs, character, up: false);
            AddUnicodeKey(inputs, character, up: true);
        }

        SendInputs(inputs);
    }

    /// <summary>Adds a Unicode packet with no virtual key, as required by SendInput.</summary>
    private static void AddUnicodeKey(List<INPUT> inputs, char character, bool up)
    {
        inputs.Add(new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION
            {
                ki = new KEYBDINPUT
                {
                    wScan = character,
                    dwFlags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0),
                }
            }
        });
    }

    private static void SendCombo(KeyCombo combo)
    {
        var inputs = new List<INPUT>();
        var m = combo.Mods;

        // When triggered via Win+C the user is physically holding Win. If the action
        // also uses Win, REUSE that held key instead of injecting our own Win events —
        // injecting (or releasing) Win while it's physically held desyncs the key and
        // can pop the Start menu. By only sending the non-Win keys here, repeated Win+C
        // taps keep firing. The dedicated Copilot key holds nothing, so we inject Win
        // normally there. Other modifiers are always injected.
        bool winHeld = IsDown(0x5B) || IsDown(0x5C);
        bool injectWin = m.HasFlag(KeyMods.Win) && !winHeld;

        if (injectWin)
        {
            AddKey(inputs, VK_LWIN, up: false);
        }

        if (m.HasFlag(KeyMods.Control))
        {
            AddKey(inputs, VK_CONTROL, up: false);
        }

        if (m.HasFlag(KeyMods.Alt))
        {
            AddKey(inputs, VK_MENU, up: false);
        }

        if (m.HasFlag(KeyMods.Shift))
        {
            AddKey(inputs, VK_SHIFT, up: false);
        }

        if (combo.VirtualKey != 0)
        {
            AddKey(inputs, (ushort)combo.VirtualKey, up: false);
            AddKey(inputs, (ushort)combo.VirtualKey, up: true);
        }

        if (m.HasFlag(KeyMods.Shift))
        {
            AddKey(inputs, VK_SHIFT, up: true);
        }

        if (m.HasFlag(KeyMods.Alt))
        {
            AddKey(inputs, VK_MENU, up: true);
        }

        if (m.HasFlag(KeyMods.Control))
        {
            AddKey(inputs, VK_CONTROL, up: true);
        }

        if (injectWin)
        {
            AddKey(inputs, VK_LWIN, up: true);
        }

        SendInputs(inputs);
    }

    /// <summary>Submits one ordered input batch and reports blocked or partial injection.</summary>
    private static void SendInputs(List<INPUT> inputs)
    {
        var batch = inputs.ToArray();
        uint sent = SendInput((uint)batch.Length, batch, Marshal.SizeOf<INPUT>());
        if (sent != batch.Length)
        {
            throw new InvalidOperationException(
                $"Windows accepted {sent} of {batch.Length} keyboard events (error {Marshal.GetLastWin32Error()}).");
        }
    }

    private static bool IsDown(int vk) => (GetAsyncKeyState(vk) & 0x8000) != 0;

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    private static void AddKey(List<INPUT> inputs, ushort vk, bool up)
    {
        uint flags = up ? KEYEVENTF_KEYUP : 0;
        if (IsExtendedKey(vk))
        {
            flags |= KEYEVENTF_EXTENDEDKEY;
        }

        inputs.Add(new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new INPUTUNION { ki = new KEYBDINPUT { wVk = vk, wScan = 0, dwFlags = flags, time = 0, dwExtraInfo = IntPtr.Zero } }
        });
    }

    private static bool IsExtendedKey(ushort vk) => vk switch
    {
        0x21 or 0x22 or 0x23 or 0x24 => true, // PageUp/PageDown/End/Home
        0x25 or 0x26 or 0x27 or 0x28 => true, // arrows
        0x2C or 0x2D or 0x2E => true,         // PrintScreen/Insert/Delete
        VK_LWIN or VK_RWIN or VK_APPS => true,
        _ => false,
    };

    // ── P/Invoke (self-contained) ─────────────────────────────────────

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_UNICODE = 0x0004;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }

    [StructLayout(LayoutKind.Explicit)]
    private struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public MOUSEINPUT mi; }

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT { public int type; public INPUTUNION u; }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
}
