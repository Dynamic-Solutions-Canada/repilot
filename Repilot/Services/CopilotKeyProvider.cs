using System.Diagnostics;

namespace Repilot.Services;

/// <summary>Whether this app is the user's assigned Copilot-key target.</summary>
public enum ProviderStatus { NotAssigned, Assigned, AssignedByPolicy, AssignedToOtherApp, SetToSearch, Unknown }

/// <summary>
/// Reports whether Repilot is the user's assigned Copilot-key provider and
/// opens the Settings page where they assign it. The actual key handling lives in
/// the separate Native-AOT key handler (RepilotKey.exe).
/// </summary>
public static class CopilotKeyProvider
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    /// <summary>Application Id of the key handler in the package manifest (&lt;Application Id="App"&gt;).</summary>
    public const string HandlerAppId = "App";

    public static bool IsPackaged { get; } = DetectPackaged();

    private static bool DetectPackaged()
    {
        try { _ = global::Windows.ApplicationModel.Package.Current.Id; return true; }
        catch { return false; }
    }

    /// <summary>The key handler's AUMID (PackageFamilyName!KeyHandler), or null when unpackaged.</summary>
    public static string? HandlerAumid()
    {
        if (!IsPackaged)
        {
            return null;
        }

        try
        {
            string family = global::Windows.ApplicationModel.Package.Current.Id.FamilyName;
            return $"{family}!{HandlerAppId}";
        }
        catch { return null; }
    }

    /// <summary>Reads the user's current Copilot-key assignment from the registry.</summary>
    public static ProviderStatus GetStatus()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\Shell\BrandedKey", false);
            var choice = key?.GetValue("BrandedKeyChoiceType") as string;
            var aumid = key?.GetValue("AppAumid") as string;

            if (string.Equals(choice, "Search", StringComparison.OrdinalIgnoreCase))
            {
                return ProviderStatus.SetToSearch;
            }

            bool isApp = string.Equals(choice, "App", StringComparison.OrdinalIgnoreCase);
            bool byPolicy = string.Equals(choice, "AppEnforcedByPolicy", StringComparison.OrdinalIgnoreCase);
            if (!isApp && !byPolicy)
            {
                return ProviderStatus.NotAssigned;
            }

            bool mine = !string.IsNullOrEmpty(aumid) &&
                        string.Equals(aumid, HandlerAumid(), StringComparison.OrdinalIgnoreCase);
            if (!mine)
            {
                return ProviderStatus.AssignedToOtherApp;
            }

            return byPolicy ? ProviderStatus.AssignedByPolicy : ProviderStatus.Assigned;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to read Copilot key assignment");
            return ProviderStatus.Unknown;
        }
    }

    /// <summary>Opens the Windows Settings page where the user assigns the Copilot key.</summary>
    public static void OpenAssignmentSettings()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:personalization-textinput-copilot-hardwarekey",
                UseShellExecute = true,
            });
        }
        catch (Exception ex) { Logger.Warn(ex, "Failed to open Copilot key settings"); }
    }
}
