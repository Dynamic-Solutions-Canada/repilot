using Repilot.ViewModels;
using WinUIAppKit;
using WinUIAppKit.Settings;
using WinUIAppKit.Theming;

namespace Repilot.Classes.Settings;

/// <summary>
/// The app's settings: %AppData%\Repilot\settings.json (redirected into package data when packaged, and
/// read by the key handler too), loaded and saved atomically by the kit's versioned
/// <see cref="JsonSettingsStore{T}"/> (CONF-3). It stamps <c>SchemaVersion</c>, runs
/// <see cref="SettingsMigrations"/> on older files after backing them up, and keeps an unreadable file as
/// <c>.bad</c>. Edits apply and save immediately.
/// </summary>
public static class SettingsManager
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private static readonly JsonSettingsStore<UserSettings> Store =
        new(Path.Combine(AppEnvironment.DataDirectory(App.DataFolderName), "settings.json"), SettingsMigrations.Steps);

    /// <summary>The loaded settings. Valid after <see cref="Restore"/>; replaced by <see cref="Reset"/>.</summary>
    public static UserSettings Current { get; private set; } = new();

    /// <summary>True when no settings file existed at launch.</summary>
    public static bool IsFirstRun { get; private set; }

    /// <summary>
    /// The load result when the file was unreadable and the user has not been told yet (CONF-2); null
    /// otherwise. Taken once by the Home page through <see cref="TakeResetNotice"/>.
    /// </summary>
    private static SettingsLoadResult<UserSettings>? _pendingResetNotice;

    /// <summary>Hand the pending reset notice to the page that shows it, once; null when there is none.</summary>
    public static SettingsLoadResult<UserSettings>? TakeResetNotice()
    {
        var notice = _pendingResetNotice;
        _pendingResetNotice = null;
        return notice;
    }

    /// <summary>
    /// Load settings (creating them on first run, upgrading an older file) and apply the saved theme.
    /// Never throws. Called after the single instance is claimed, so a second launch never touches the file.
    /// </summary>
    public static void Restore()
    {
        var result = Store.Load();
        Current = result.Settings;
        Current.Action ??= new();
        Current.LanguageOverride ??= "";
        IsFirstRun = result.Outcome == SettingsLoadOutcome.FirstRun;
        _pendingResetNotice = result.NeedsResetNotice ? result : null;
        Logger.Info("Settings: {Outcome} (file schema {FileVersion}, backup {Backup})",
            result.Outcome, result.FileSchemaVersion?.ToString() ?? "none", result.BackupPath ?? "none");
        AppTheme.Set(Current.AppTheme);
    }

    /// <summary>
    /// "Reset settings" (CONF-4): the store keeps the current file as <c>settings.json.&lt;UTC time&gt;.reset</c>
    /// and writes defaults, so the Copilot key's action becomes "Nothing", the theme follows Windows at
    /// once, and the language follows Windows from the next launch (Repilot's text cannot relabel an
    /// open window). The key's assignment in Windows Settings is Windows' and stays as it is.
    /// </summary>
    public static void Reset()
    {
        Current = Store.Reset().Settings;
        Current.Action ??= new();
        Current.LanguageOverride ??= "";
        AppTheme.Set(Current.AppTheme);
    }

    /// <summary>Save the current settings; failures are logged by the store, never thrown.</summary>
    public static void Save() => Store.Save(Current);
}
