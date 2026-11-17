using Repilot.Classes.Settings;
using Repilot.Pages;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.IO;
using System.Linq;
using WinRT.Interop;
using WinUIAppKit.Theming;
using WinUIAppKit.Windowing;
using static Repilot.Classes.NativeMethods;

namespace Repilot;

/// <summary>The settings window — the app's only window. Plain on-demand UI.</summary>
public sealed partial class SettingsWindow : Window
{
    private static SettingsWindow? _instance;

    public SettingsWindow()
    {
        if (_instance != null)
        {
            SetForegroundWindow(WindowNative.GetWindowHandle(_instance));
            Close();
            return;
        }

        InitializeComponent();
        _instance = this;
        Closed += (s, e) => _instance = null;

        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        var hwnd = WindowNative.GetWindowHandle(this);
        var wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(wndId);

        // Sharp taskbar and title bar icons at the window's scaling (the kit's WindowIcon).
        string iconPath = App.IconPath;
        WindowIcon.Apply(this, iconPath);
        if (File.Exists(iconPath))
        {
            LoadTitleBarIcon(iconPath);
        }

        uint dpi = GetDpiForWindow(hwnd);
        double scale = dpi / 96.0;
        var settings = SettingsManager.Current;

        if (settings.SettingsWindowWidth > 0 && settings.SettingsWindowHeight > 0)
        {
            appWindow.Resize(new global::Windows.Graphics.SizeInt32(settings.SettingsWindowWidth, settings.SettingsWindowHeight));
            appWindow.Move(new global::Windows.Graphics.PointInt32(settings.SettingsWindowX, settings.SettingsWindowY));
        }
        else
        {
            int width = (int)(820 * scale), height = (int)(680 * scale);
            appWindow.Resize(new global::Windows.Graphics.SizeInt32(width, height));
            GetCursorPos(out var cursorPt);
            var monitor = MonitorFromPoint(cursorPt, MONITOR_DEFAULTTONEAREST);
            var mi = new MONITORINFOEX { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(monitor, ref mi))
            {
                int cx = mi.rcWork.Left + (mi.rcWork.Right - mi.rcWork.Left - width) / 2;
                int cy = mi.rcWork.Top + (mi.rcWork.Bottom - mi.rcWork.Top - height) / 2;
                appWindow.Move(new global::Windows.Graphics.PointInt32(cx, cy));
            }
        }

        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        ContentFrame.Navigate(typeof(HomePage));

        // The saved theme, applied now and again whenever the Settings page changes it.
        AppTheme.Set((AppThemeChoice)settings.AppTheme);
        AppTheme.Track(this);

        Closed += SettingsWindow_Closed;
    }

    public static SettingsWindow? GetCurrent() => _instance;

    public void NavigateTo(Type pageType)
    {
        ContentFrame.Navigate(pageType);
        if (pageType == typeof(SettingsPage))
        {
            RootNavigation.SelectedItem = RootNavigation.SettingsItem;
            return;
        }
        foreach (var item in RootNavigation.MenuItems.OfType<NavigationViewItem>())
        {
            if (item.Tag is string tag && GetPageTypeFromTag(tag) == pageType) { RootNavigation.SelectedItem = item; return; }
        }

        foreach (var item in RootNavigation.FooterMenuItems.OfType<NavigationViewItem>())
        {
            if (item.Tag is string tag && GetPageTypeFromTag(tag) == pageType) { RootNavigation.SelectedItem = item; return; }
        }
    }

    private void RootNavigation_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        Type? pageType = args.IsSettingsSelected
            ? typeof(SettingsPage)
            : (args.SelectedItem as NavigationViewItem)?.Tag is string tag ? GetPageTypeFromTag(tag) : null;
        if (pageType == null || ContentFrame.Content?.GetType() == pageType)
        {
            return;
        }

        ContentFrame.Navigate(pageType);
    }

    private static Type? GetPageTypeFromTag(string tag) => tag switch
    {
        "HomePage" => typeof(HomePage),
        "ActionPage" => typeof(ActionPage),
        "SettingsPage" => typeof(SettingsPage),
        "PromotedAppsPage" => typeof(TechnicallyReal.Promo.PromotedAppsPage),
        "AboutPage" => typeof(AboutPage),
        _ => null
    };

    private void LoadTitleBarIcon(string icoPath)
    {
        try { TitleBarIcon.Source = new BitmapImage(new Uri(icoPath)); }
        catch { /* leave empty */ }
    }

    private void SettingsWindow_Closed(object sender, WindowEventArgs e)
    {
        var hwnd = WindowNative.GetWindowHandle(this);
        var wndId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
        var appWindow = AppWindow.GetFromWindowId(wndId);
        var settings = SettingsManager.Current;

        bool isMaximized = appWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Maximized;
        if (!isMaximized)
        {
            settings.SettingsWindowX = appWindow.Position.X;
            settings.SettingsWindowY = appWindow.Position.Y;
            settings.SettingsWindowWidth = appWindow.Size.Width;
            settings.SettingsWindowHeight = appWindow.Size.Height;
        }
        SettingsManager.SaveSettings();
    }
}
