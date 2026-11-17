using Repilot.Classes;
using Repilot.Classes.Settings;
using Repilot.Helpers;
using Repilot.Models;
using Repilot.Services;
using Repilot.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Repilot.Pages;

public sealed partial class ActionPage : Page
{
    private bool _loading;
    private ShortcutRecorder? _recorder;
    private string _comboBeforeRecording = "";

    private List<InstalledApp> _allApps = new();

    // Catalog rows with text resolved once in the current app language.
    private readonly List<WindowsFunctionItem> _functionItems =
        WindowsFunctionCatalog.All.Select(f => new WindowsFunctionItem(f)).ToList();
    private bool _appsLoaded;

    private CopilotActionData Action => SettingsManager.Current.Action;

    public ActionPage()
    {
        InitializeComponent();
        RecordButton.Content = UiStrings.Get("RecordShortcut");
        BuildFunctionList(null);
        LoadFromAction();
    }

    // ── Mode selection (SelectorBar) ─────────────────────────────────

    private void ModeBar_SelectionChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        if (_loading)
        {
            return;
        }

        var newType = (ModeBar.SelectedItem?.Tag as string) switch
        {
            "KeyCombo" => CopilotActionType.KeyCombo,
            "LaunchApp" => CopilotActionType.LaunchApp,
            "WindowsFunction" => CopilotActionType.WindowsFunction,
            "Text" => CopilotActionType.Text,
            _ => CopilotActionType.None,
        };
        if (newType == Action.Type)
        {
            return; // ignore spurious/programmatic re-selection
        }

        Action.Type = newType;              // the mode bar is the ONLY writer of Type
        SettingsManager.Save();
        ShowPanelFor(newType);
    }

    private void LoadFromAction()
    {
        _loading = true;
        var a = Action;

        SelectBarItem(ModeBar, a.Type switch
        {
            CopilotActionType.KeyCombo => "KeyCombo",
            CopilotActionType.LaunchApp => "LaunchApp",
            CopilotActionType.WindowsFunction => "WindowsFunction",
            CopilotActionType.Text => "Text",
            _ => "None",
        });

        ComboDisplay.Text = a.Combo is { IsEmpty: false } c ? c.ToString() : UiStrings.Get("NotSet");
        TextBox.Text = a.Text;

        // Launch box shows only custom (non-app) targets.
        bool isApp = a.LaunchPath.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase);
        LaunchPathBox.Text = isApp ? "" : a.LaunchPath;
        LaunchArgsBox.Text = a.LaunchArguments;

        FunctionSearch.Text = "";
        BuildFunctionList(null);
        if (a.Type == CopilotActionType.WindowsFunction)
        {
            SelectFunctionInList(a.WindowsFunctionId);
        }

        ShowPanelFor(a.Type);
        _loading = false;

        if (a.Type == CopilotActionType.LaunchApp)
        {
            _ = EnsureAppsLoadedAsync();
        }
    }

    private static void SelectBarItem(SelectorBar bar, string tag)
    {
        foreach (var item in bar.Items)
        {
            if (item.Tag as string == tag) { bar.SelectedItem = item; return; }
        }
    }

    private void ShowPanelFor(CopilotActionType type)
    {
        var V = Microsoft.UI.Xaml.Visibility.Visible;
        var C = Microsoft.UI.Xaml.Visibility.Collapsed;
        ComboPanel.Visibility = type == CopilotActionType.KeyCombo ? V : C;
        LaunchPanel.Visibility = type == CopilotActionType.LaunchApp ? V : C;
        FunctionPanel.Visibility = type == CopilotActionType.WindowsFunction ? V : C;
        NonePanel.Visibility = type == CopilotActionType.None ? V : C;
        TextPanel.Visibility = type == CopilotActionType.Text ? V : C;

        if (type == CopilotActionType.LaunchApp)
        {
            _ = EnsureAppsLoadedAsync();
        }
    }

    // ── App chooser ──────────────────────────────────────────────────

    private async Task EnsureAppsLoadedAsync()
    {
        if (_appsLoaded)
        {
            return;
        }

        _appsLoaded = true;
        _allApps = await InstalledAppsService.GetAppsAsync();
        FilterApps(AppSearch.Text);
        InstalledAppsService.LoadIcons(_allApps, DispatcherQueue);
        PreselectCurrentApp();
    }

    private void FilterApps(string? query)
    {
        IEnumerable<InstalledApp> items = _allApps;
        if (!string.IsNullOrWhiteSpace(query))
        {
            items = items.Where(a => a.Name.Contains(query.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        AppList.ItemsSource = items.ToList();
    }

    private void PreselectCurrentApp()
    {
        if (Action.Type != CopilotActionType.LaunchApp)
        {
            return;
        }

        var match = _allApps.FirstOrDefault(a =>
            string.Equals(a.LaunchPath, Action.LaunchPath, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            return;
        }

        _loading = true;
        if (AppList.ItemsSource is IEnumerable<InstalledApp> shown && shown.Contains(match))
        {
            AppList.SelectedItem = match;
        }

        _loading = false;
    }

    private void AppSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        FilterApps(sender.Text);
    }

    private void AppList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        if (AppList.SelectedItem is not InstalledApp app)
        {
            return;
        }

        if (string.Equals(app.LaunchPath, Action.LaunchPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }
        // Type is owned by the mode bar; this only sets the target.
        Action.LaunchPath = app.LaunchPath;
        Action.LaunchDisplayName = app.Name;
        Action.LaunchArguments = "";
        SettingsManager.Save();
        _loading = true; LaunchPathBox.Text = ""; _loading = false; // the two inputs don't fight
    }

    // ── Custom shortcut capture (low-level hook, suppresses keys) ─────

    private void RecordButton_Click(object sender, RoutedEventArgs e)
    {
        if (_recorder?.IsRecording == true) { _recorder.Stop(); EndRecordingUi(restore: true); return; }
        _recorder ??= CreateRecorder();
        _comboBeforeRecording = ComboDisplay.Text;
        ComboDisplay.Text = UiStrings.Get("RecordPrompt");
        ComboBorder.BorderBrush = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
        ComboBorder.BorderThickness = new Thickness(2);
        RecordButton.Content = UiStrings.Get("Recording");
        RecordButton.Focus(FocusState.Programmatic);
        _recorder.Start();
    }

    private ShortcutRecorder CreateRecorder()
    {
        var r = new ShortcutRecorder(DispatcherQueue);
        r.ModifiersChanged += mods =>
        {
            if (_recorder?.IsRecording != true)
            {
                return;
            }

            ComboDisplay.Text = mods == KeyMods.None ? UiStrings.Get("RecordPrompt") : $"{new KeyCombo(mods, 0)} + …";
        };
        r.Captured += combo =>
        {
            Action.Combo = combo; // Type is owned by the mode bar (Shortcut)
            SettingsManager.Save();
            EndRecordingUi(restore: false);
            ComboDisplay.Text = combo.ToString();
        };
        r.Cancelled += () => EndRecordingUi(restore: true);
        return r;
    }

    private void EndRecordingUi(bool restore)
    {
        RecordButton.Content = UiStrings.Get("RecordShortcut");
        ComboBorder.BorderBrush = (Brush)Application.Current.Resources["ControlStrokeColorDefaultBrush"];
        ComboBorder.BorderThickness = new Thickness(1);
        if (restore)
        {
            ComboDisplay.Text = _comboBeforeRecording;
        }
    }

    private void RecordButton_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_recorder?.IsRecording == true) { _recorder.Stop(); EndRecordingUi(restore: true); }
    }

    private void ClearComboButton_Click(object sender, RoutedEventArgs e)
    {
        Action.Combo = new KeyCombo();
        ComboDisplay.Text = UiStrings.Get("NotSet");
        SettingsManager.Save();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e) => _recorder?.Stop();

    /// <summary>Save text verbatim, including spaces, without changing the selected action type.</summary>
    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        Action.Text = TextBox.Text;
        SettingsManager.Save();
    }

    // ── Custom file / link ───────────────────────────────────────────

    private void LaunchPathBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        if (string.IsNullOrEmpty(LaunchPathBox.Text))
        {
            return; // don't clobber an app selection
        }

        Action.LaunchPath = LaunchPathBox.Text;
        Action.LaunchDisplayName = FriendlyTarget(LaunchPathBox.Text);
        SettingsManager.Save();
        _loading = true; AppList.SelectedItem = null; _loading = false; // typing a path clears app selection
    }

    private void LaunchArgsBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        Action.LaunchArguments = LaunchArgsBox.Text;
        SettingsManager.Save();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e) => _ = BrowseAsync();

    private async Task BrowseAsync()
    {
        var picker = new FileOpenPicker();
        picker.FileTypeFilter.Add("*");
        var window = SettingsWindow.GetCurrent();
        if (window == null)
        {
            return;
        }

        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(window));
        var file = await picker.PickSingleFileAsync();
        if (file == null)
        {
            return;
        }

        LaunchPathBox.Text = file.Path; // triggers TextChanged → saves
    }

    private static string FriendlyTarget(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        if (path.Contains("://") || (path.Contains(':') && path.StartsWith("ms-")))
        {
            return path;
        }

        try { return Path.GetFileNameWithoutExtension(path); }
        catch { return path; }
    }

    // ── Windows-function catalog ─────────────────────────────────────

    private void BuildFunctionList(string? filter)
    {
        IEnumerable<WindowsFunctionItem> items = _functionItems;
        if (!string.IsNullOrWhiteSpace(filter))
        {
            string f = filter.Trim();
            items = items.Where(x => x.Matches(f));
        }

        // GroupBy keeps the catalog's group order, so headings stay in their curated sequence.
        var groups = items.GroupBy(x => x.Group)
            .Select(g => new Grouping<string, WindowsFunctionItem>(g.Key, g)).ToList();
        FunctionList.ItemsSource = new CollectionViewSource { IsSourceGrouped = true, Source = groups }.View;
    }

    private void FunctionSearch_TextChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        if (args.Reason != AutoSuggestionBoxTextChangeReason.UserInput)
        {
            return;
        }

        BuildFunctionList(sender.Text);
    }

    private void FunctionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading)
        {
            return;
        }

        if (FunctionList.SelectedItem is not WindowsFunctionItem item)
        {
            return;
        }

        if (item.Function.Id == Action.WindowsFunctionId)
        {
            return;
        }

        Action.WindowsFunctionId = item.Function.Id; // Type is owned by the mode bar
        SettingsManager.Save();
    }

    private void SelectFunctionInList(string id)
    {
        if (WindowsFunctionCatalog.TryGet(id) == null)
        {
            return;
        }

        if (FunctionList.ItemsSource is Microsoft.UI.Xaml.Data.ICollectionView view)
        {
            foreach (var group in view)
            {
                if (group is IEnumerable<WindowsFunctionItem> g)
                {
                    foreach (var item in g)
                    {
                        if (item.Function.Id == id) { FunctionList.SelectedItem = item; return; }
                    }
                }
            }
        }
    }
}
