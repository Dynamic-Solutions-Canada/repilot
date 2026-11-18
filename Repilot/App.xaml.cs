using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Repilot.Classes;
using Repilot.Classes.Settings;
using Repilot.Services;
using WinUIAppKit;
using WinUIAppKit.Hosting;
using WinUIAppKit.Localization;
using WinUIAppKit.Logging;
using WinUIAppKit.Windowing;

namespace Repilot;

/// <summary>
/// Application entry point (WinUI 3). This process is only the settings UI: it is launched on demand
/// (Start tile or "Open in Settings"), is never resident, and is not in the Copilot key path. The key
/// itself is handled by the separate RepilotKey.exe.
/// </summary>
public partial class App : Application
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>The data folder under %AppData%, shared with the key handler. Permanent once shipped.</summary>
    internal const string DataFolderName = "Repilot";

    /// <summary>The single-instance mutex. Permanent: older versions claim the same name.</summary>
    private const string InstanceMutexName = "Repilot.Settings";

    public static DispatcherQueue MainDispatcherQueue { get; private set; } = null!;

    /// <summary>Path to the app icon (used for window/title-bar/about images).</summary>
    public static string IconPath => Path.Combine(AppContext.BaseDirectory, "Resources", "Repilot.ico");

    /// <summary>Path to the 256 px app icon PNG, for the settings shell and the About page.</summary>
    public static string IconPngPath => Path.Combine(AppContext.BaseDirectory, "Resources", "Repilot.png");

    /// <summary>
    /// Startup in the order the kit's gotchas require: logging first, then the single instance (before
    /// settings are read, so a second launch never touches the file), then crash logging, settings and
    /// the language. The language override must be in place before any XAML or string resource loads,
    /// so this happens here rather than in <see cref="OnLaunched"/>.
    /// </summary>
    public App()
    {
        AppLogging.UseDataFolder(DataFolderName);

        if (!SingleInstance.TryClaim(InstanceMutexName))
        {
            // Restore and bring forward the running copy's window, using this launch's foreground right,
            // then end this launch off the UI thread (the kit's safe exit for a second launch).
            SingleInstance.BringRunningInstanceForward(InstanceMutexName);
            SingleInstance.ExitThisLaunch();
        }

        AppBootstrap.InstallCrashLogging(this);

        SettingsManager.Restore();
        AppLanguage.ApplySaved();

        // Repilot's own text is .resw (LOC-6); UiText supplies only the kit's shared labels, in the same
        // language as the rest of the app.
        UiText.Configure(typeof(App).Assembly, "Repilot.Catalogs.", AppLanguage.Supported, AppLanguage.AppliedAtStartup);

        // The shared "Our other apps" page translates itself; point it at the same language.
        TechnicallyReal.Promo.PromoLocalization.LocaleProvider = () => UiText.Locale;

        InitializeComponent();
    }

    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        MainDispatcherQueue = DispatcherQueue.GetForCurrentThread();
        ActionExecutor.ErrorSink = ex => Logger.Error(ex, "Action execution failed");

        m_window = new SettingsWindow();
        m_window.Activate();

        // Record the window so a second launch can bring it forward (the kit's non-resident single instance).
        SingleInstance.RegisterWindow(WindowChrome.Hwnd(m_window));
    }

    internal Window? m_window;
}
