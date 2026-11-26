using Repilot.Classes;
using Repilot.Classes.Settings;
using Repilot.Pages;
using Repilot.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
        Shell.MenuItems.Add(CreateNavItem(HomeTag, "NavHome", new SymbolIcon(Symbol.Home)));
        Shell.MenuItems.Add(CreateNavItem(ActionTag, "NavAction", new FontIcon { Glyph = "\uE765" }));

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

    /// <summary>
    /// One navigation item built in code. Its automation ID is its string key (<c>NavHome</c>,
    /// <c>NavAction</c>): unlike XAML elements it has no <c>x:Name</c> to supply one, and UI automation
    /// (the Store screenshot capture) must find it by something that does not change with the
    /// interface language.
    /// </summary>
    /// <param name="tag">The page's navigation tag.</param>
    /// <param name="key">The string key of its label, also its automation ID.</param>
    /// <param name="icon">Its icon.</param>
    private static NavigationViewItem CreateNavItem(string tag, string key, IconElement icon)
    {
        var item = new NavigationViewItem
        {
            Content = UiStrings.Get(key),
            Tag = tag,
            Icon = icon,
        };
        AutomationProperties.SetAutomationId(item, key);
        return item;
    }

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
