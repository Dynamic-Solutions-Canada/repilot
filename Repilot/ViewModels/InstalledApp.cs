using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml.Media;

namespace Repilot.ViewModels;

/// <summary>One installed app from shell:AppsFolder, for the app picker.</summary>
public partial class InstalledApp : ObservableObject
{
    public string Name { get; }

    /// <summary>The shell path from AppsFolder: either an AUMID or a file path.</summary>
    public string Aumid { get; }

    private bool IsFilePath => Aumid.Length >= 2 && (Aumid[1] == ':' || Aumid.StartsWith(@"\\"));

    /// <summary>
    /// Launch/icon target. AUMID apps go through shell:AppsFolder; file-path entries
    /// (some desktop apps) are launched/iconified directly.
    /// </summary>
    public string LaunchPath => IsFilePath ? Aumid : $@"shell:AppsFolder\{Aumid}";

    /// <summary>Icon, assigned asynchronously after enumeration.</summary>
    [ObservableProperty]
    public partial ImageSource? Icon { get; set; }

    public InstalledApp(string name, string aumid)
    {
        Name = name;
        Aumid = aumid;
    }
}
