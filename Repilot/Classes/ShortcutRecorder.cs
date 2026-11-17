using Repilot.Models;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using static Repilot.Classes.NativeMethods;

namespace Repilot.Classes;

/// <summary>
/// Captures a keyboard chord by installing a temporary low-level keyboard hook that
/// SUPPRESSES the keys while recording — so system shortcuts (Alt+Tab, Win+Tab, …)
/// don't actually fire and steal focus. Reports modifiers live and the final combo.
/// Only active between <see cref="Start"/> and <see cref="Stop"/>.
/// </summary>
internal sealed class ShortcutRecorder
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    // Modifier virtual keys (low-level hook reports L/R-specific codes).
    private const int VK_LWIN = 0x5B, VK_RWIN = 0x5C, VK_ESCAPE = 0x1B;

    private readonly DispatcherQueue _dispatcher;
    private readonly DispatcherTimer _timeout;
    private IntPtr _hook;
    private LowLevelKeyboardProc? _proc; // keep alive
    private KeyMods _mods;

    /// <summary>Fired (on the UI thread) as modifiers are pressed/released, for live display.</summary>
    public event Action<KeyMods>? ModifiersChanged;
    /// <summary>Fired (on the UI thread) when a full chord is captured.</summary>
    public event Action<KeyCombo>? Captured;
    /// <summary>Fired (on the UI thread) when recording is cancelled (Esc / timeout / Stop).</summary>
    public event Action? Cancelled;

    public bool IsRecording => _hook != IntPtr.Zero;

    public ShortcutRecorder(DispatcherQueue dispatcher)
    {
        _dispatcher = dispatcher;
        _timeout = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _timeout.Tick += (s, e) => Cancel();
    }

    public void Start()
    {
        if (_hook != IntPtr.Zero)
        {
            return;
        }

        _mods = KeyMods.None;
        _proc = HookProc;
        _hook = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            Logger.Error("Failed to install recording hook (err {0})", Marshal.GetLastWin32Error());
            _proc = null;
            _dispatcher.TryEnqueue(() => Cancelled?.Invoke());
            return;
        }
        _timeout.Start();
    }

    public void Stop()
    {
        _timeout.Stop();
        if (_hook == IntPtr.Zero)
        {
            return;
        }

        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        _proc = null;
    }

    private void Cancel()
    {
        if (!IsRecording)
        {
            return;
        }

        Stop();
        _dispatcher.TryEnqueue(() => Cancelled?.Invoke());
    }

    private IntPtr HookProc(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode == HC_ACTION)
        {
            int msg = (int)wParam;
            var data = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            int vk = (int)data.vkCode;
            bool isDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            var mod = ModifierFor(vk);

            if (isDown)
            {
                if (mod != KeyMods.None)
                {
                    _mods |= mod;
                    var snapshot = _mods;
                    _dispatcher.TryEnqueue(() => ModifiersChanged?.Invoke(snapshot));
                }
                else if (vk == VK_ESCAPE && _mods == KeyMods.None)
                {
                    Cancel();
                }
                else
                {
                    var combo = new KeyCombo(_mods, vk);
                    Stop();
                    _dispatcher.TryEnqueue(() => Captured?.Invoke(combo));
                }
            }
            else if (mod != KeyMods.None)
            {
                _mods &= ~mod;
                var snapshot = _mods;
                _dispatcher.TryEnqueue(() => ModifiersChanged?.Invoke(snapshot));
            }

            return 1; // suppress: the chord must not actually fire while recording
        }
        return CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    private static KeyMods ModifierFor(int vk) => vk switch
    {
        0x10 or 0xA0 or 0xA1 => KeyMods.Shift,   // SHIFT / LSHIFT / RSHIFT
        0x11 or 0xA2 or 0xA3 => KeyMods.Control, // CONTROL / LCONTROL / RCONTROL
        0x12 or 0xA4 or 0xA5 => KeyMods.Alt,     // MENU / LMENU / RMENU
        VK_LWIN or VK_RWIN => KeyMods.Win,
        _ => KeyMods.None,
    };
}
