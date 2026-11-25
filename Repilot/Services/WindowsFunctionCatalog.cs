using Repilot.Models;
using System.Collections.Generic;
using System.Linq;
using static Repilot.Models.KeyMods;
using static Repilot.Models.KeyNames;

namespace Repilot.Services;

/// <summary>
/// Curated, grouped catalog of Windows functions a user can bind the Copilot
/// key to by name, instead of having to know the underlying shortcut. Each
/// entry runs either a synthesized keystroke or a shell launch.
/// </summary>
public static class WindowsFunctionCatalog
{
    private const int VK_OEM_PLUS = 0xBB, VK_OEM_PERIOD = 0xBE;

    private static KeyCombo K(KeyMods mods, int vk) => new(mods, vk);
    private static int C(char c) => c; // letter/digit -> virtual key

    /// <summary>All catalog entries, in display order.</summary>
    public static IReadOnlyList<WindowsFunction> All { get; } = new List<WindowsFunction>
    {
        // ── Window management ────────────────────────────────────────
        new() { Id = "task-view", Name = "Task View", Group = "Window management", Glyph = "",
            Description = "Show all open windows and virtual desktops.", Combo = K(Win, VK_TAB) },
        new() { Id = "snap-left", Name = "Snap window left", Group = "Window management", Glyph = "",
            Description = "Dock the active window to the left half.", Combo = K(Win, VK_LEFT) },
        new() { Id = "snap-right", Name = "Snap window right", Group = "Window management", Glyph = "",
            Description = "Dock the active window to the right half.", Combo = K(Win, VK_RIGHT) },
        new() { Id = "maximize", Name = "Maximize window", Group = "Window management", Glyph = "",
            Description = "Maximize the active window.", Combo = K(Win, VK_UP) },
        new() { Id = "minimize", Name = "Minimize window", Group = "Window management", Glyph = "",
            Description = "Minimize the active window.", Combo = K(Win, VK_DOWN) },
        new() { Id = "show-desktop", Name = "Show desktop", Group = "Window management", Glyph = "",
            Description = "Minimize everything and show the desktop.", Combo = K(Win, C('D')) },
        new() { Id = "snap-layouts", Name = "Snap layouts", Group = "Window management", Glyph = "",
            Description = "Open the snap-layouts picker for the active window.", Combo = K(Win, C('Z')) },
        new() { Id = "new-desktop", Name = "New virtual desktop", Group = "Window management", Glyph = "",
            Description = "Create a new virtual desktop.", Combo = K(Win | Control, C('D')) },
        new() { Id = "next-desktop", Name = "Next virtual desktop", Group = "Window management", Glyph = "",
            Description = "Switch to the next virtual desktop.", Combo = K(Win | Control, VK_RIGHT) },
        new() { Id = "prev-desktop", Name = "Previous virtual desktop", Group = "Window management", Glyph = "",
            Description = "Switch to the previous virtual desktop.", Combo = K(Win | Control, VK_LEFT) },

        // ── System ───────────────────────────────────────────────────
        new() { Id = "lock-pc", Name = "Lock PC", Group = "System", Glyph = "",
            Description = "Lock the computer.", Combo = K(Win, C('L')) },
        new() { Id = "file-explorer", Name = "File Explorer", Group = "System", Glyph = "",
            Description = "Open a new File Explorer window.", Combo = K(Win, C('E')) },
        new() { Id = "run-dialog", Name = "Run dialog", Group = "System", Glyph = "",
            Description = "Open the Run dialog.", Combo = K(Win, C('R')) },
        new() { Id = "task-manager", Name = "Task Manager", Group = "System", Glyph = "",
            Description = "Open Task Manager.", Combo = K(Control | Shift, VK_ESCAPE) },
        new() { Id = "power-user-menu", Name = "Quick Link menu (Win+X)", Group = "System", Glyph = "",
            Description = "Open the power-user / Quick Link menu.", Combo = K(Win, C('X')) },
        new() { Id = "project", Name = "Project / second screen", Group = "System", Glyph = "",
            Description = "Open the projection (display mode) flyout.", Combo = K(Win, C('P')) },
        new() { Id = "cast-connect", Name = "Cast to a device", Group = "System", Glyph = "",
            Description = "Open the Connect (wireless display) flyout.", Combo = K(Win, C('K')) },

        // ── Capture & recording ──────────────────────────────────────
        new() { Id = "screen-snip", Name = "Screen snip (Snip & Sketch)", Group = "Capture & recording", Glyph = "",
            Description = "Start a rectangular screen snip.", Combo = K(Win | Shift, C('S')) },
        new() { Id = "print-screen", Name = "Print Screen", Group = "Capture & recording", Glyph = "",
            Description = "Capture the whole screen to the clipboard.", Combo = K(None, VK_SNAPSHOT) },
        new() { Id = "game-bar", Name = "Xbox Game Bar", Group = "Capture & recording", Glyph = "",
            Description = "Open the Game Bar overlay.", Combo = K(Win, C('G')) },
        new() { Id = "record-screen", Name = "Record screen", Group = "Capture & recording", Glyph = "",
            Description = "Start/stop screen recording via Game Bar.", Combo = K(Win | Alt, C('R')) },

        // ── Notifications & input ────────────────────────────────────
        new() { Id = "notifications", Name = "Notification center", Group = "Notifications & input", Glyph = "",
            Description = "Open the notification center and calendar.", Combo = K(Win, C('N')) },
        new() { Id = "context-menu", Name = "Menu key (context menu)", Group = "Notifications & input", Glyph = "",
            Description = "Open the right-click menu for whatever has focus, like the old Menu key.", Combo = K(None, VK_APPS) },
        new() { Id = "quick-settings", Name = "Quick settings", Group = "Notifications & input", Glyph = "",
            Description = "Open the quick-settings flyout (Wi-Fi, volume, etc.).", Combo = K(Win, C('A')) },
        new() { Id = "clipboard-history", Name = "Clipboard history", Group = "Notifications & input", Glyph = "",
            Description = "Open clipboard history.", Combo = K(Win, C('V')) },
        new() { Id = "emoji-panel", Name = "Emoji & symbols panel", Group = "Notifications & input", Glyph = "",
            Description = "Open the emoji, kaomoji, and symbols picker.", Combo = K(Win, VK_OEM_PERIOD) },
        new() { Id = "voice-typing", Name = "Voice typing", Group = "Notifications & input", Glyph = "",
            Description = "Start voice typing / dictation.", Combo = K(Win, C('H')) },
        new() { Id = "widgets", Name = "Widgets board", Group = "Notifications & input", Glyph = "",
            Description = "Open the widgets panel.", Combo = K(Win, C('W')) },

        // ── Accessibility ────────────────────────────────────────────
        new() { Id = "magnifier", Name = "Magnifier", Group = "Accessibility", Glyph = "",
            Description = "Turn on the Magnifier / zoom in.", Combo = K(Win, VK_OEM_PLUS) },
        new() { Id = "narrator", Name = "Narrator", Group = "Accessibility", Glyph = "",
            Description = "Toggle the Narrator screen reader.", Combo = K(Win | Control, VK_RETURN) },
        new() { Id = "on-screen-keyboard", Name = "On-Screen Keyboard", Group = "Accessibility", Glyph = "",
            Description = "Toggle the on-screen keyboard.", Combo = K(Win | Control, C('O')) },
        new() { Id = "color-filters", Name = "Color filters", Group = "Accessibility", Glyph = "",
            Description = "Toggle color filters.", Combo = K(Win | Control, C('C')) },
        new() { Id = "live-captions", Name = "Live captions", Group = "Accessibility", Glyph = "",
            Description = "Toggle system-wide live captions.", Combo = K(Win | Control, C('L')) },

        // ── Search ───────────────────────────────────────────────────
        new() { Id = "search", Name = "Search", Group = "Search", Glyph = "",
            Description = "Open Windows Search.", Combo = K(Win, C('S')) },

        // ── Settings pages (shell-launched) ──────────────────────────
        new() { Id = "settings-home", Name = "All Settings", Group = "Settings", Glyph = "",
            Description = "Open the Settings app.", Combo = K(Win, C('I')) },
        new() { Id = "settings-display", Name = "Display settings", Group = "Settings", Glyph = "",
            Description = "Open Display settings.", ShellTarget = "ms-settings:display" },
        new() { Id = "settings-sound", Name = "Sound settings", Group = "Settings", Glyph = "",
            Description = "Open Sound settings.", ShellTarget = "ms-settings:sound" },
        new() { Id = "settings-bluetooth", Name = "Bluetooth & devices", Group = "Settings", Glyph = "",
            Description = "Open Bluetooth & devices settings.", ShellTarget = "ms-settings:bluetooth" },
        new() { Id = "settings-apps", Name = "Installed apps", Group = "Settings", Glyph = "",
            Description = "Open the installed-apps settings.", ShellTarget = "ms-settings:appsfeatures" },
    };

    private static readonly Dictionary<string, WindowsFunction> ById =
        All.ToDictionary(f => f.Id);

    public static WindowsFunction? TryGet(string id) =>
        id != null && ById.TryGetValue(id, out var f) ? f : null;

    public static string NameFor(string id) => TryGet(id)?.Name ?? id;

    /// <summary>Catalog entries grouped by their <see cref="WindowsFunction.Group"/>, in catalog order.</summary>
    public static IEnumerable<IGrouping<string, WindowsFunction>> Grouped() =>
        All.GroupBy(f => f.Group);
}
