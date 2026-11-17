using Repilot.Classes;
using Repilot.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

namespace Repilot.Services;

/// <summary>
/// Enumerates installed apps via the Shell.Application COM object over
/// shell:AppsFolder (covers Store, desktop, and PWA apps), and streams in their
/// icons. Results are cached for the session.
/// </summary>
public static class InstalledAppsService
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
    private static List<InstalledApp>? _cache;

    public static async Task<List<InstalledApp>> GetAppsAsync()
    {
        return _cache ??= await RunStaAsync(Enumerate);
    }

    /// <summary>Runs work on a dedicated STA thread (Shell.Application is apartment-threaded).</summary>
    private static Task<T> RunStaAsync<T>(Func<T> func)
    {
        var tcs = new TaskCompletionSource<T>();
        var t = new Thread(() =>
        {
            try { tcs.SetResult(func()); }
            catch (Exception ex) { Logger.Warn(ex, "STA enumerate failed"); tcs.SetResult(default!); }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
        return tcs.Task;
    }

    private static List<InstalledApp> Enumerate()
    {
        var list = new List<InstalledApp>();
        try
        {
            var shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null)
            {
                return list;
            }

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic folder = shell.NameSpace("shell:AppsFolder");
            if (folder == null)
            {
                return list;
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (dynamic item in folder.Items())
            {
                string name = item.Name ?? "";
                string aumid = item.Path ?? "";
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(aumid))
                {
                    continue;
                }

                if (!seen.Add(aumid))
                {
                    continue;
                }

                list.Add(new InstalledApp(name, aumid));
            }
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to enumerate installed apps");
        }
        return list.OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    /// <summary>Extracts icons on a background STA thread and assigns them on the UI thread.</summary>
    public static void LoadIcons(IReadOnlyList<InstalledApp> apps, DispatcherQueue dispatcher)
    {
        var t = new Thread(() =>
        {
            foreach (var app in apps)
            {
                if (app.Icon != null)
                {
                    continue;
                }

                ShellIcons.IconPixels? px;
                try { px = ShellIcons.Extract(app.LaunchPath, 32); }
                catch { px = null; }
                if (px == null)
                {
                    continue;
                }

                dispatcher.TryEnqueue(() =>
                {
                    try
                    {
                        var wb = new WriteableBitmap(px.Width, px.Height);
                        using var s = wb.PixelBuffer.AsStream();
                        s.Write(px.Bgra, 0, px.Bgra.Length);
                        app.Icon = wb;
                    }
                    catch { /* skip this icon */ }
                });
            }
        });
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }
}
