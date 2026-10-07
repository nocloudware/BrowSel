using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;

namespace BrowSel;

public sealed partial class MainWindow : Window
{
    readonly List<Row> _rows = new();
    List<Editors.Ed> _editors = new();
    readonly List<(int Index, string Exe, Image Img)> _editorIcons = new();
    readonly Dictionary<string, BitmapSource> _icons = new();
    Settings _cfg = Settings.Load(Discover.Browsers);

    public MainWindow()
    {
        InitializeComponent();

        // The saved language wins; otherwise the one from the operating system.
        if (_cfg.Lang != "") S.Lang = _cfg.Lang;

        // Translucent backdrop, like the native Windows 11 apps
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(titleBar);

        txtUrl.Text = Program.Url;
        ApplyText();
        Build();

        // The About window would hold the app alive with no picker to click.
        Closed += (_, _) => { _about?.Close(); Save(); };
        root.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape) Close(); };
        // The HWND only exists after Activate: that is why the size is set on the first real launch.
        Activated += First;
    }

    // Every visible string comes from here so it can follow the chosen language.
    void ApplyText()
    {
        txtPick.Text = S.T("pickTitle");
        empty.Text = S.T("empty");
        ToolTipService.SetToolTip(btnSettings, S.T("settings"));
        ToolTipService.SetToolTip(btnAbout, S.T("about"));
        ToolTipService.SetToolTip(btnDonate, S.T("donate"));
        dlgSettings.Title = S.T("dialogTitle");
        dlgSettings.PrimaryButtonText = S.T("save");
        dlgSettings.CloseButtonText = S.T("cancel");
        lblEditor.Text = S.T("editor");
        lblLang.Text = S.T("langLabel");
        ToolTipService.SetToolTip(editorBox, S.T("editorTip"));
    }

    // The credits window, kept so closing the picker takes it with it: it would otherwise keep the
    // app alive with nothing to pick.
    AboutWindow? _about;

    // Activate or it never shows: a WinUI window is invisible until it is activated. The settings
    // dialog does not need this because it lives inside this window, it only needs ShowAsync.
    void OnAboutClick(object sender, RoutedEventArgs e)
    {
        _about?.Close();
        _about = new AboutWindow(Program.Owner);
        _about.Activate();
    }

    void OnDonateClick(object sender, RoutedEventArgs e)
    {
        try { Discover.OpenPage("https://nocloudware.com/donate.html"); }
        catch (Exception ex) { Program.Error(ex); }
    }

    void First(object sender, WindowActivatedEventArgs e)
    {
        Activated -= First;
        var id = Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        Program.Owner = WinRT.Interop.WindowNative.GetWindowHandle(this); // message boxes stay in front
        var app = AppWindow.GetFromWindowId(id);
        var area = DisplayArea.GetFromWindowId(id, DisplayAreaFallback.Primary).WorkArea;
        const int w = 560, h = 640;
        app.MoveAndResize(new RectInt32((area.Width - w) / 2, (area.Height - h) / 2, w, h));

        // leaves room for the window buttons (close/minimize) so the gear does not sit underneath them
        root.Loaded += (_, _) =>
            titleBar.Padding = new Thickness(16, 0, (app.TitleBar.RightInset / root.XamlRoot.RasterizationScale) + 4, 0);
    }

    // Redraws the whole tree from scratch: used at start and every time settings are confirmed.
    void Rebuild()
    {
        _rows.Clear();
        tree.Visibility = Visibility.Visible;
        empty.Visibility = Visibility.Collapsed;
        Build();
    }

    void Build()
    {
        int total = 0;
        foreach (var b in Discover.Browsers.Where(b => _cfg.Browsers.Contains(b.Name) && Discover.FindExe(b) != null))
        {
            List<Entry> list;
            try { list = Discover.Load(b); } catch { list = new(); } // one broken browser must not take the picker down

            var row = new Row { Name = b.Name, Expanded = _cfg.Expanded.Contains(b.Name) };
        foreach (var e in list.OrderBy(x => x.Label))
        {
            total++;
            row.Kids.Add(new Row { Entry = e });
        }

        _rows.Add(row);
    }

        // Two standing options at the end: open the link in the editor, or just copy it.
        _rows.Add(new Row { Action = "editor" });
        _rows.Add(new Row { Action = "copy" });

        if (total == 0)
        {
            tree.Visibility = Visibility.Collapsed;
            empty.Visibility = Visibility.Visible;
        }
        else _ = LoadIcons();

        Reflow();
    }

    static Brush Open() => Application.Current.RequestedTheme == ApplicationTheme.Dark
        ? new SolidColorBrush(Color.FromArgb(0xFF, 0x54, 0xB0, 0x54))
        : new SolidColorBrush(Color.FromArgb(0xFF, 0x0F, 0x7B, 0x0F));

    static Brush Off() => Application.Current.RequestedTheme == ApplicationTheme.Dark
        ? new SolidColorBrush(Color.FromArgb(0x8A, 0xFF, 0xFF, 0xFF))
        : new SolidColorBrush(Color.FromArgb(0x8A, 0x00, 0x00, 0x00));

    // WinUI 3 does not draw a TreeViewNode Content: the list is built flat and the collapse is handled
    // by hand, which we need anyway to persist it. Every row builds NEW controls on each
    // refresh: recycling the same ones while replacing the ItemsSource breaks the XAML engine natively.
    void Reflow()
    {
        // Every row is wrapped in a NEW object: if the same Row is reused, x:Bind does not read
        // View again and the view keeps whatever was drawn the first time (old icons, old chevron).
        var vis = new List<Item>();
        void Walk(List<Row> rs)
        {
            foreach (var r in rs)
            {
                vis.Add(new Item
                {
                    Row = r,
                    View = r.Action is not null ? ActionView(r) : r.Entry is not null ? LeafView(r.Entry) : HeadView(r),
                });
                if (r.Expanded) Walk(r.Kids);
            }
        }
        Walk(_rows);
        tree.ItemsSource = vis;
    }

    // Browser header: chevron, icon and name.
    UIElement HeadView(Row r)
    {
        var chev = new FontIcon
        {
            Glyph = r.Expanded ? "\uE70D" : "\uE76C",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = r.Kids.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
        };
        // Real browser icon; if Windows cannot extract one, the globe glyph stays.
        var glyph = new FontIcon { Glyph = "\uE774", FontSize = 14, VerticalAlignment = VerticalAlignment.Center };
        var img = new Image { Width = 18, Height = 18, Stretch = Stretch.Uniform };
        if (_icons.TryGetValue(r.Name!, out var src)) { img.Source = src; glyph.Visibility = Visibility.Collapsed; }
        else img.Visibility = Visibility.Collapsed;

        var name = new TextBlock { Text = r.Name, VerticalAlignment = VerticalAlignment.Center };
        var head = new Grid { ColumnDefinitions =
        {
            new ColumnDefinition { Width = new GridLength(14) },
            new ColumnDefinition { Width = new GridLength(18) },
            new ColumnDefinition(),
        }, ColumnSpacing = 8 };
        Grid.SetColumn(chev, 0); Grid.SetColumn(glyph, 1); Grid.SetColumn(img, 1); Grid.SetColumn(name, 2);
        head.Children.Add(chev); head.Children.Add(glyph); head.Children.Add(img); head.Children.Add(name);
        return head;
    }

    // The two standing rows: icon + label, no chevron, no dot.
    UIElement ActionView(Row r)
    {
        var icon = new FontIcon
        {
            Glyph = r.Action == "editor" ? "\uE70F" : "\uE8C8",
            FontSize = 14,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var txt = new TextBlock
        {
            Text = r.Action == "editor" ? S.T("openInEditor") : S.T("copyLink"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var grid = new Grid { ColumnSpacing = 8, Margin = new Thickness(22, 0, 0, 0),
            ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(18) }, new ColumnDefinition() } };
        Grid.SetColumn(txt, 1);
        grid.Children.Add(icon);
        grid.Children.Add(txt);
        return grid;
    }

    static UIElement LeafView(Entry e)
    {
        // Status dot drawn, not a font character: it looks the same in any theme.
        var dot = new Border
        {
            Width = 8,
            Height = 8,
            CornerRadius = new CornerRadius(4),
            VerticalAlignment = VerticalAlignment.Center,
            Background = e.Open ? Open() : Off(),
        };
        var txt = new TextBlock { Text = e.Label, VerticalAlignment = VerticalAlignment.Center };
        // Deeper than the browser rows (which start their name at 48) so the hierarchy reads at a glance.
var leaf = new Grid { ColumnSpacing = 10, Margin = new Thickness(38, 0, 0, 0),
            ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(8) }, new ColumnDefinition() } };
        Grid.SetColumn(txt, 1);
        leaf.Children.Add(dot);
        leaf.Children.Add(txt);
        if (e.Open) ToolTipService.SetToolTip(leaf, S.T("detectedBy") + e.Why);
        return leaf;
    }

    // Icons are read from disk: loaded after the window is shown so they do not delay it.
    async Task LoadIcons()
    {
        foreach (var b in Discover.Browsers)
        {
            try
            {
                var exe = Discover.FindExe(b);
                if (exe == null) continue;
                var file = await StorageFile.GetFileFromPathAsync(exe);
                using var th = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 64, ThumbnailOptions.ResizeThumbnail);
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(th);
                _icons[b.Name] = bmp;
            }
            catch { /* no icon: the glyph stays. One broken browser cannot cut the rest. */ }
        }
        Reflow(); // repaints now that the icons are in
    }

    // A click on a browser row opens or collapses it; a click on a profile opens the link.
    void OnSelectionChanged(TreeView sender, TreeViewSelectionChangedEventArgs e)
    {
        if (e.AddedItems.Count == 0 || e.AddedItems[0] is not Item it) return;
        var r = it.Row!;
        if (r.Entry is not null)
        {
            Discover.Launch(r.Entry, Program.Url);
            Close();
            return;
        }
        if (r.Action is not null)
        {
            DoAction(r.Action);
            Close();
            return;
        }
        if (r.Kids.Count == 0) return;
        r.Expanded = !r.Expanded;
        // The XAML engine breaks natively (not a catchable exception) if you replace the
        // TreeView list from inside SelectionChanged. Deferred until after the event.
        DispatcherQueue.TryEnqueue(Reflow);
    }

    // Editor and Copy both leave the app without opening the link in a browser.
    void DoAction(string action)
    {
        if (action == "copy")
        {
            var pkg = new DataPackage();
            pkg.SetText(Program.Url);
            Clipboard.SetContent(pkg);
            // Without Flush the content is lost: the process exits right after.
            Clipboard.Flush();
            return;
        }
        Editors.Open(_cfg.Editor != "" ? _cfg.Editor : Editors.DefaultId(), Program.Url);
    }

    void BuildEditors()
    {
        editorBox.Items.Clear();
        var all = Editors.All();
        foreach (var ed in all)
        {
            var img = new Image { Width = 16, Height = 16, Stretch = Stretch.Uniform, VerticalAlignment = VerticalAlignment.Center };
            var txt = new TextBlock { Text = ed.Name, VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(img, 0);
            Grid.SetColumn(txt, 1);
            editorBox.Items.Add(new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(16) }, new ColumnDefinition() },
                Children = { img, txt },
            });
            _editorIcons.Add((editorBox.Items.Count - 1, ed.Exe, img));
        }

        _editors = all;
        var want = _cfg.Editor != "" ? _cfg.Editor : Editors.DefaultId();
        var pick = all.FindIndex(e => string.Equals(e.Id, want, StringComparison.OrdinalIgnoreCase));
        editorBox.SelectedIndex = pick >= 0 ? pick : 0;

        _ = LoadEditorIcons();
    }

    // Same trick as the browser icons: read the executable thumbnail from disk.
    async Task LoadEditorIcons()
    {
        foreach (var (index, exe, img) in _editorIcons)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(exe);
                using var th = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, 64, ThumbnailOptions.ResizeThumbnail);
                var bmp = new BitmapImage();
                await bmp.SetSourceAsync(th);
                img.Source = bmp;
            }
            catch { /* no icon: the name alone is enough */ }
        }
        _editorIcons.Clear();
    }

    async void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        // Only browsers that are really installed: otherwise the picker shows orphan branches.
        checks.Children.Clear();
        foreach (var b in Discover.Browsers.Where(b => Discover.FindExe(b) != null))
        {
            var box = new CheckBox { Content = b.Name, IsChecked = _cfg.Browsers.Contains(b.Name) };
            box.Checked += OnPreview;
            box.Unchecked += OnPreview;
            checks.Children.Add(box);
        }

        BuildEditors();
        BuildLanguages();

        var wasLang = S.Lang;
        var wasBrowsers = _cfg.Browsers;
        var wasEditor = _cfg.Editor;

        if (await dlgSettings.ShowAsync() == ContentDialogResult.Primary)
        {
            var editor = editorBox.SelectedIndex >= 0 && editorBox.SelectedIndex < _editors.Count
                ? _editors[editorBox.SelectedIndex].Id
                : _cfg.Editor;

            if (Ticked().Length == 0)
            {
                Program.Msg(S.T("needOne"));
                S.Lang = wasLang;
                _cfg = _cfg with { Browsers = wasBrowsers };
            }
            else _cfg = _cfg with { Lang = S.Lang, Editor = editor };

            _cfg.Save();
            ApplyText();
            return;
        }

        // Cancel: put everything back the way it was.
        S.Lang = wasLang;
        _cfg = _cfg with { Browsers = wasBrowsers, Editor = wasEditor };
        ApplyText();
        Rebuild();
    }

    string[] Ticked() => checks.Children.OfType<CheckBox>()
        .Where(c => c.IsChecked == true).Select(c => (string)c.Content).ToArray();

    // Live preview: every change repaints the picker right away. Cancel undoes it.
    void OnPreview(object sender, RoutedEventArgs e)
    {
        var keep = Ticked();
        if (keep.Length == 0) return; // cannot preview an empty picker
        _cfg = _cfg with { Browsers = keep };
        Rebuild();
    }

    void OnPreviewLang(object sender, SelectionChangedEventArgs e)
    {
        var i = langBox.SelectedIndex;
        if (i < 0 || i >= S.Codes.Length || S.Codes[i] == S.Lang) return;
        S.Lang = S.Codes[i];
        // Only the picker text: touching the dialog strings moves the open list under the cursor.
        txtPick.Text = S.T("pickTitle");
        empty.Text = S.T("empty");
        Rebuild();
    }

    void BuildLanguages()
    {
        langBox.Items.Clear();
        foreach (var code in S.Codes)
        {
            var flag = new Border { Child = Flag.Of(code), VerticalAlignment = VerticalAlignment.Center };
            var name = new TextBlock { Text = S.Name(code), VerticalAlignment = VerticalAlignment.Center };
            Grid.SetColumn(flag, 0);
            Grid.SetColumn(name, 1);
            langBox.Items.Add(new Grid
            {
                ColumnSpacing = 10,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(34) },
                    new ColumnDefinition(),
                },
                Children = { flag, name },
            });
        }
        langBox.SelectedIndex = Array.IndexOf(S.Codes, S.Lang);
    }

    // Expanded branches and ticked browsers are remembered for the next
    void Save() =>
        (_cfg with { Expanded = _rows.Where(r => r.Expanded).Select(r => r.Name!).ToArray() }).Save();
}

// State of a tree row. Entry present = leaf (profile).
sealed class Row
{
    public string? Name { get; set; }
    public Entry? Entry { get; set; }
    public List<Row> Kids { get; set; } = new();
    public bool Expanded { get; set; }
    public string? Action { get; set; }
}

// What gets drawn in a TreeView row. Rebuilt from scratch on every refresh so the
// data binding gets evaluated again.
sealed class Item
{
    public Row Row { get; set; } = null!;
    public UIElement View { get; set; } = null!;
}
