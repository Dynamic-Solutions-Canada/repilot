using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Repilot.Services;

namespace TechnicallyReal.Promo;

/// <summary>
/// Repilot's localization adapter for the shared "Our other apps" page from the promo
/// submodule. The submodule keeps loading, caching, ordering, icons and destinations; this
/// only swaps known English copy for its translation. Product names, links and any remote
/// copy not listed here stay exactly as published.
/// </summary>
public sealed partial class PromotedAppsPage
{
    /// <summary>Exact upstream English text mapped to its Repilot string resource.</summary>
    private static readonly Dictionary<string, string> PromoCopy = new(StringComparer.Ordinal)
    {
        ["Our other apps"] = "PromoTitle",
        ["More from Dynamic Solutions Canada on the Microsoft Store."] = "PromoSubtitle",
        ["Visit website"] = "PromoVisitWebsite",
        ["View in Store"] = "PromoViewInStore",
        ["Manage your 3D printers, one or a whole farm, from any device."] = "PromoPrintStream",
        ["Launch apps and websites from your tray on Windows and Linux. Windows is available in the Store; Linux uses AppImage."] = "PromoLittleLauncher",
        ["ImmichDrive brings your self-hosted Immich photo and video library straight into Windows File Explorer as a native cloud drive. Your whole timeline appears as files you can..."] = "PromoImmichDrive",
        ["Reminders that don't give up."] = "PromoPersistent",
    };

    private bool _promoAttached;
    private bool _promoApplying;
    private long _promoSourceCallback;

    /// <summary>
    /// Translates the page headings and current cards, and every later card refresh.
    /// Safe to call more than once; it attaches only once per page instance.
    /// </summary>
    internal void EnableLocalization()
    {
        if (_promoAttached) return;
        _promoAttached = true;

        TranslateHeadings();
        TranslateCards();

        // The page swaps ItemsSource after its background refresh; translate that set too.
        _promoSourceCallback = AppList.RegisterPropertyChangedCallback(
            ItemsControl.ItemsSourceProperty, (_, _) => TranslateCards());
        Unloaded += PromoUnloaded;
    }

    /// <summary>Translates the two headings in the page's own content panel.</summary>
    private void TranslateHeadings()
    {
        if (Content is not ScrollViewer { Content: Panel panel }) return;

        foreach (var heading in panel.Children.OfType<TextBlock>())
        {
            heading.Text = Translate(heading.Text);
        }
    }

    /// <summary>Replaces the card list with copies whose blurb and button label are translated.</summary>
    private void TranslateCards()
    {
        // Our own assignment below re-enters through the ItemsSource callback.
        if (_promoApplying) return;
        if (AppList.ItemsSource is not IEnumerable<PromotedApp> apps) return;

        _promoApplying = true;
        try
        {
            AppList.ItemsSource = apps.Select(app => new PromotedApp
            {
                Name = app.Name,
                Destination = app.Destination,
                Icon = app.Icon,
                Blurb = Translate(app.Blurb),
                ActionLabel = Translate(app.ActionLabel),
            }).ToList();
        }
        finally
        {
            _promoApplying = false;
        }
    }

    /// <summary>Returns the translation of known copy, or <paramref name="value"/> unchanged.</summary>
    private static string Translate(string value)
        => PromoCopy.TryGetValue(value, out string? key) ? UiStrings.Get(key, value) : value;

    private void PromoUnloaded(object sender, RoutedEventArgs e)
    {
        Unloaded -= PromoUnloaded;
        AppList.UnregisterPropertyChangedCallback(ItemsControl.ItemsSourceProperty, _promoSourceCallback);
        _promoAttached = false;
    }
}
