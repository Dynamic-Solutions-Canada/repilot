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

        // Mica, the custom title bar, sharp icons, a minimum size, the saved placement (or the default
        // size, centred) and the theme, from the kit's shared settings-window setup.
        string iconPath = App.IconPath;
        WindowChrome.SetUpSettingsWindow(this, AppTitleBar, iconPath, SettingsManager.Current.SettingsWindow,
            defaultWidthDip: 820, defaultHeightDip: 680);
        if (File.Exists(iconPath))
        {
            LoadTitleBarIcon(iconPath);
        }

        RootNavigation.SelectedItem = RootNavigation.MenuItems[0];
        ContentFrame.Navigate(typeof(HomePage));

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

    /// <summary>Saves the window's normal position and size (not when minimized or maximized).</summary>
    private void SettingsWindow_Closed(object sender, WindowEventArgs e)
    {
        if (WindowChrome.CapturePlacement(this) is { } placement)
        {
            SettingsManager.Current.SettingsWindow = placement;
        }

        SettingsManager.Save();
    }
}
