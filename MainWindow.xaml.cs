using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Windows.UI;

namespace BrowSel;

public sealed partial class MainWindow : Window
{
    readonly List<Row> _rows = new();
    readonly Dictionary<string, BitmapSource> _icons = new();
    Settings _cfg = Settings.Load(Discover.Browsers);

    public MainWindow()
    {
        InitializeComponent();

        // Translucent backdrop, like the native Windows 11 apps
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(titleBar);

        txtUrl.Text = Program.Url;
        Build();

        Closed += (_, _) => Save();
        root.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape) Close(); };
        // The HWND only exists after Activate: that is why the size is set on the first real launch.
        Activated += First;
    }

    void First(object sender, WindowActivatedEventArgs e)
    {
        Activated -= First;
        var id = Win32Interop.GetWindowIdFromWindow(WinRT.Interop.WindowNative.GetWindowHandle(this));
        var app = AppWindow.GetFromWindowId(id);
        var area = DisplayArea.GetFromWindowId(id, DisplayAreaFallback.Primary).WorkArea;
        const int w = 560, h = 640;
        app.MoveAndResize(new RectInt32((area.Width - w) / 2, (area.Height - h) / 2, w, h));

        // leaves room for the window buttons (close/minimize) so the gear does not sit underneath them
        root.Loaded += (_, _) =>
            titleBar.Padding = new Thickness(16, 0, (app.TitleBar.RightInset / root.XamlRoot.RasterizationScale) + 4, 0);
    }

    void Build()
    {
        int total = 0;
        foreach (var b in Discover.Browsers.Where(b => _cfg.Browsers.Contains(b.Name)))
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
                vis.Add(new Item { Row = r, View = r.Entry is null ? HeadView(r) : LeafView(r.Entry) });
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
        var leaf = new Grid { ColumnSpacing = 10, Margin = new Thickness(22, 0, 0, 0),
            ColumnDefinitions = { new ColumnDefinition { Width = new GridLength(8) }, new ColumnDefinition() } };
        Grid.SetColumn(txt, 1);
        leaf.Children.Add(dot);
        leaf.Children.Add(txt);
        if (e.Open) ToolTipService.SetToolTip(leaf, "Detected by: " + e.Why);
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
        Reflow(); // repinta ahora que ya hay iconos
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
        if (r.Kids.Count == 0) return;
        r.Expanded = !r.Expanded;
        // The XAML engine breaks natively (not a catchable exception) if you replace the
        // TreeView list from inside SelectionChanged. Deferred until after the event.
        DispatcherQueue.TryEnqueue(Reflow);
    }

    async void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        checks.Children.Clear();
        foreach (var b in Discover.Browsers)
            checks.Children.Add(new CheckBox { Content = b.Name, IsChecked = _cfg.Browsers.Contains(b.Name) });

        if (await dlgSettings.ShowAsync() != ContentDialogResult.Primary) return; // cancel changes nothing

        var keep = checks.Children.OfType<CheckBox>()
            .Where(c => c.IsChecked == true).Select(c => (string)c.Content).ToArray();
        if (keep.Length == 0)
        {
            Program.Msg("Leave at least one browser selected.");
            return;
        }
        (_cfg = _cfg with { Browsers = keep }).Save();
        Program.Msg("Saved. It takes effect the next time you open a link.");
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
}

// What gets drawn in a TreeView row. Rebuilt from scratch on every refresh so the
// data binding gets evaluated again.
sealed class Item
{
    public Row Row { get; set; } = null!;
    public UIElement View { get; set; } = null!;
}
