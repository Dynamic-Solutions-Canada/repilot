using Repilot.Services;
using System.Globalization;
using System.IO;
using System.Threading;
using Repilot.Models;

namespace RepilotKey;

/// <summary>
/// The Copilot key handler: the ONLY thing in the keypress path. Windows launches
/// this on a key press; it reads settings.json, runs the configured action, and exits.
/// Native single-file: fast startup, no resident process.
///
/// Windows auto-repeats the launch while the key is held, so a short debounce keeps a
/// hold from spamming the action; one press runs it once.
/// </summary>
internal static class Program
{
    private const double RepeatGapMs = 700; // launches closer than this are auto-repeat (held)

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            // Hold-release protocol activation (if a device ever sends it) is a no-op.
            if (string.Join(' ', args).ToLowerInvariant().Contains("state=up"))
            {
                return 0;
            }

            var settings = LoadSettings();

            // Serialize overlapping auto-repeat processes so the debounce is consistent.
            using var mutex = new Mutex(false, "Repilot.HandlerState");
            bool owned = false;
            try { owned = mutex.WaitOne(TimeSpan.FromSeconds(2)); }
            catch (AbandonedMutexException) { owned = true; }
            try
            {
                var now = DateTime.UtcNow;
                double sinceLast = (now - ReadLastLaunch()).TotalMilliseconds;
                WriteLastLaunch(now); // always update so continued holding keeps suppressing
                if (sinceLast < 0 || sinceLast > RepeatGapMs)
                {
                    Log($"Run -> {settings.Action.Type}");
                    ActionExecutor.RunSync(settings.Action);
                }
            }
            finally { if (owned) { try { mutex.ReleaseMutex(); } catch { } } }
            return 0;
        }
        catch (Exception ex)
        {
            Log("ERROR: " + ex);
            return 1;
        }
    }

    private static string SettingsDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Repilot");

    private static DateTime ReadLastLaunch()
    {
        try
        {
            string path = Path.Combine(SettingsDir, "repeat-state");
            if (!File.Exists(path))
            {
                return DateTime.MinValue;
            }

            return new DateTime(long.Parse(File.ReadAllText(path).Trim(), CultureInfo.InvariantCulture), DateTimeKind.Utc);
        }
        catch { return DateTime.MinValue; }
    }

    private static void WriteLastLaunch(DateTime when)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            File.WriteAllText(Path.Combine(SettingsDir, "repeat-state"), when.Ticks.ToString(CultureInfo.InvariantCulture));
        }
        catch { /* best-effort */ }
    }

    private static KeySettings LoadSettings()
    {
        string path = Path.Combine(SettingsDir, "settings.json");
        if (!File.Exists(path))
        {
            return new KeySettings();
        }

        return KeySettings.Parse(File.ReadAllText(path));
    }

    private static void Log(string message)
    {
        try
        {
            Directory.CreateDirectory(SettingsDir);
            string path = Path.Combine(SettingsDir, "key-handler.log");
            if (File.Exists(path) && new FileInfo(path).Length > 64 * 1024)
            {
                File.Delete(path);
            }

            File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}");
        }
        catch { /* best-effort */ }
    }
}
