using Repilot.Classes.Settings;
using Repilot.Services;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using System.IO;
using static Repilot.Classes.NativeMethods;

namespace Repilot;

/// <summary>
/// Application entry point (WinUI 3). This process is ONLY the settings UI — it is
/// launched on demand (Start tile / "Open in Settings"), never resident, and is not
/// in the Copilot-key path. The key itself is handled by the tiny Native-AOT
/// RepilotKey.exe.
/// </summary>
public partial class App : Application
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public static DispatcherQueue MainDispatcherQueue { get; private set; } = null!;

    /// <summary>Path to the app icon (used for window/title-bar/about images).</summary>
    public static string IconPath => Path.Combine(AppContext.BaseDirectory, "Resources", "Repilot.ico");

    private static readonly Mutex Singleton = new(true, "Repilot.Settings");

    public App()
    {
        // The language override must be in place before any XAML or string resource loads,
        // so settings are read here rather than in OnLaunched.
        SettingsManager.RestoreSettings();
        Classes.AppLanguage.ApplySaved();

        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Single settings window: if one is already open, surface it and exit.
        if (!Singleton.WaitOne(TimeSpan.Zero, true))
        {
            IntPtr existing = FindWindow(null, "Repilot");
            if (existing != IntPtr.Zero)
            {
                SetForegroundWindow(existing);
            }

            Environment.Exit(0);
        }

        AppDomain.CurrentDomain.UnhandledException += (s, a) =>
        {
            Logger.Error(a.ExceptionObject as Exception, "Unhandled exception");
            NLog.LogManager.Flush();
        };
        UnhandledException += (s, e) =>
        {
            Logger.Error(e.Exception, "Unhandled UI exception");
            NLog.LogManager.Flush();
            e.Handled = true;
        };

        ActionExecutor.ErrorSink = ex => Logger.Error(ex, "Action execution failed");

        m_window = new SettingsWindow();
        m_window.Activate();
    }

    internal Window? m_window;
}
