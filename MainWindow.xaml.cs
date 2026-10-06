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

        // Fondo translucido, al estilo de las apps nativas de Windows 11
        SystemBackdrop = new MicaBackdrop();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(titleBar);

        txtUrl.Text = Program.Url;
        Build();

        Closed += (_, _) => Save();
        root.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape) Close(); };
        // El HWND recien existe despues de Activate: por eso el tamano se ajusta en el primer arranque real.
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

        // deja libre el ancho de los botones de la ventana (cerrar/minimizar) para que el engranaje no quede debajo
        root.Loaded += (_, _) =>
            titleBar.Padding = new Thickness(16, 0, (app.TitleBar.RightInset / root.XamlRoot.RasterizationScale) + 4, 0);
    }

    void Build()
    {
        int total = 0;
        foreach (var b in Discover.Browsers.Where(b => _cfg.Browsers.Contains(b.Name)))
        {
            List<Entry> list;
            try { list = Discover.Load(b); } catch { list = new(); } // un navegador roto no tumba el selector

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

    // WinUI 3 no dibuja el Content de un TreeViewNode: la lista se arma plana y el colapso se maneja
    // a mano, que ademas ya hace falta para persistirlo. Cada fila dibuja controles NUEVOS en cada
    // refresco: reciclar los mismos al reemplazar la ItemsSource rompe el motor XAML de forma nativa.
    void Reflow()
    {
        // Cada fila se envuelve en un objeto NUEVO: si se reutiliza el mismo Row, x:Bind no vuelve a
        // leer View y la vista queda con lo que se dibujo la primera vez (iconos viejos, chevron viejo).
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

    // Cabecera del navegador: chevron, icono y nombre.
    UIElement HeadView(Row r)
    {
        var chev = new FontIcon
        {
            Glyph = r.Expanded ? "\uE70D" : "\uE76C",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
            Visibility = r.Kids.Count > 0 ? Visibility.Visible : Visibility.Collapsed,
        };
        // Icono real del navegador; si Windows no lo puede sacar, queda el glifo de globo.
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
        // Punto de estado dibujado, no un caracter de fuente: asi se ve igual en cualquier tema.
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
        if (e.Open) ToolTipService.SetToolTip(leaf, "Detectado por: " + e.Why);
        return leaf;
    }

    // Los iconos se leen del disco: se cargan despues de mostrar la ventana para no retrasarla.
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
            catch { /* sin icono: se queda el glifo. Un navegador roto no puede cortar el resto. */ }
        }
        Reflow(); // repinta ahora que ya hay iconos
    }

    // Un clic en la fila del navegador abre o colapsa; un clic en un perfil abre el enlace.
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
        // El motor XAML se rompe nativo (no es una excepcion que se pueda capturar) si se reemplaza la
        // lista del TreeView desde dentro de SelectionChanged. Se difiere a despues del evento.
        DispatcherQueue.TryEnqueue(Reflow);
    }

    async void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        checks.Children.Clear();
        foreach (var b in Discover.Browsers)
            checks.Children.Add(new CheckBox { Content = b.Name, IsChecked = _cfg.Browsers.Contains(b.Name) });

        if (await dlgSettings.ShowAsync() != ContentDialogResult.Primary) return; // cancelar no toca nada

        var keep = checks.Children.OfType<CheckBox>()
            .Where(c => c.IsChecked == true).Select(c => (string)c.Content).ToArray();
        if (keep.Length == 0)
        {
            Program.Msg("Deja al menos un navegador.");
            return;
        }
        (_cfg = _cfg with { Browsers = keep }).Save();
        Program.Msg("Guardado. Se aplica la proxima vez que abras un enlace.");
    }

    // Rama abierta/cerrada + navegadores marcados quedan para la proxima
    void Save() =>
        (_cfg with { Expanded = _rows.Where(r => r.Expanded).Select(r => r.Name!).ToArray() }).Save();
}

// Estado de una fila del arbol. Entry presente = hoja (perfil).
sealed class Row
{
    public string? Name { get; set; }
    public Entry? Entry { get; set; }
    public List<Row> Kids { get; set; } = new();
    public bool Expanded { get; set; }
}

// Lo que se dibuja en un renglon del TreeView. Se crea de cero en cada refresco para que el
// enlace de datos vuelva a evaluarse.
sealed class Item
{
    public Row Row { get; set; } = null!;
    public UIElement View { get; set; } = null!;
}
