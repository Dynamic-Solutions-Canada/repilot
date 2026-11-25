using Repilot.Classes;
using Repilot.Classes.Settings;
using Repilot.Pages;
using Repilot.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WinUIAppKit.Pages;
using WinUIAppKit.Windowing;

namespace Repilot;

/// <summary>The settings window, the app's only window. Plain on-demand UI.</summary>
public sealed partial class SettingsWindow : Window
{
    private static SettingsWindow? _instance;

    /// <summary>The tag of the Home page, Repilot's first menu item.</summary>
    private const string HomeTag = "home";

    /// <summary>The tag of the Action page, where the Copilot key's action is chosen.</summary>
    private const string ActionTag = "action";

    public SettingsWindow()
    {
        if (_instance != null)
        {
            WindowChrome.BringToFront(_instance);
            Close();
            return;
        }

        InitializeComponent();
        _instance = this;
        Closed += (s, e) => _instance = null;

        Shell.Caption = "Repilot";
        Shell.IconPngPath = App.IconPngPath;
        Shell.MenuItems.Add(new NavigationViewItem
        {
            Content = UiStrings.Get("NavHome"),
            Tag = HomeTag,
            Icon = new SymbolIcon(Symbol.Home),
        });
        Shell.MenuItems.Add(new NavigationViewItem
        {
            Content = UiStrings.Get("NavAction"),
            Tag = ActionTag,
            Icon = new FontIcon { Glyph = "\uE765" },
        });

        // Mica, the custom title bar, sharp icons, a minimum size, the saved placement (or the default
        // size, centred) and the theme, from the kit's shared settings-window setup.
        WindowChrome.SetUpSettingsWindow(this, Shell.TitleBar, App.IconPath, SettingsManager.Current.SettingsWindow,
            defaultWidthDip: 820, defaultHeightDip: 680);

        Shell.PageSelected += tag =>
        {
            // The kit's About page takes Repilot's AboutInfo; every other page needs no parameter.
            object? parameter = tag == SettingsShell.AboutTag ? AboutContent.Create() : null;
            Shell.ContentFrame.Navigate(PageFor(tag), parameter);
        };
        Shell.Select(HomeTag);

        Closed += SettingsWindow_Closed;
    }

    public static SettingsWindow? GetCurrent() => _instance;

    /// <summary>Opens the Action page and selects its menu item, for the Home page's "Change" button.</summary>
    public void ShowActionPage() => Shell.Select(ActionTag);

    /// <summary>The page for a menu tag: Repilot's own pages, then the shell's Settings, About and "Our other apps".</summary>
    private static Type PageFor(string tag) => tag switch
    {
        ActionTag => typeof(ActionPage),
        SettingsShell.SettingsTag => typeof(SettingsPage),
        SettingsShell.AboutTag => typeof(WinUIAppKit.Pages.AboutPage),
        SettingsShell.OtherAppsTag => typeof(TechnicallyReal.Promo.PromotedAppsPage),
        _ => typeof(HomePage),
    };

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
