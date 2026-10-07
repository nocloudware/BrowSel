using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics;
using Windows.UI;

namespace BrowSel;

// The credits window, in the style of the one the other Nocloudware apps show.
//
// WinUI 3 has no modal window, so the picker is made one: its input is switched off while this
// window is up. Without that the picker stays clickable behind the dialog, which is the one thing
// a modal is for.
public sealed partial class AboutWindow : Window
{
    // Same components as THIRD_PARTY_NOTICES.txt, so the two cannot drift apart silently.
    static readonly (string Name, string Url)[] Libraries =
    {
        ("Microsoft.WindowsAppSDK", "https://github.com/microsoft/WindowsAppSDK/blob/main/LICENSE"),
        ("Microsoft.Windows.SDK.BuildTools", "https://github.com/microsoft/WindowsAppSDK-Samples/blob/main/LICENSE"),
        ("System.Management", "https://github.com/dotnet/runtime/blob/main/LICENSE.TXT"),
    };

    // Fixed size, like the original: it is a card of credits, not a document.
    const int W = 520, H = 600;

    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool EnableWindow(IntPtr h, bool on);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RectInt32 r);

    public AboutWindow(IntPtr ownerHandle)
    {
        InitializeComponent();

        EnableWindow(ownerHandle, false);

        SystemBackdrop = new MicaBackdrop();
        // The icon sits next to the exe, so it is loaded by path: ms-appx:/// points at a package folder
        // this app does not have.
        imgIcon.Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "BrowSel.ico")));
        ApplyText();

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        Program.Owner = hwnd; // the update messages belong to this window while it is up
        HideFromTaskbar(hwnd);

        var area = DisplayArea.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd),
                                               DisplayAreaFallback.Primary).WorkArea;
        GetWindowRect(ownerHandle, out var here);
        AppWindow.GetFromWindowId(Win32Interop.GetWindowIdFromWindow(hwnd))
            .MoveAndResize(new RectInt32(
                Clamp(here.X + (here.Width - W) / 2, area.X, area.Width - W),
                Clamp(here.Y + (here.Height - H) / 2, area.Y, area.Height - H),
                W, H));

        root.KeyDown += (_, e) => { if (e.Key == Windows.System.VirtualKey.Escape) Close(); };
        Closed += (_, _) =>
        {
            EnableWindow(ownerHandle, true);
            Program.Owner = ownerHandle;
            SetForegroundWindow(ownerHandle);
        };
    }

    // Keeps the credits window on screen when the picker sits near an edge.
    static int Clamp(int wanted, int from, int room) => Math.Max(from, Math.Min(wanted, from + room));

    // No taskbar button: a credits window next to the picker should not look like a second app.
    // This is what ShowInTaskbar="False" does in the WPF version.
    static void HideFromTaskbar(IntPtr h) =>
        SetWindowLong(h, -20, (GetWindowLong(h, -20) | 0x80) & ~0x40000);

    void ApplyText()
    {
        // Three parts: the assembly keeps a 0 revision, and 1.1 says nothing about which build this is.
        lblVersion.Text = "v" + Update.Current.ToString(3);
        btnUpdate.Content = S.T("checkUpdates");
        btnClose.Content = S.T("close");
        BuildCredits();
    }

    void BuildCredits()
    {
        credits.Children.Clear();

        var dark = Application.Current.RequestedTheme == ApplicationTheme.Dark;
        var muted = new SolidColorBrush(dark
            ? Color.FromArgb(0xFF, 0xC5, 0xC5, 0xC5)
            : Color.FromArgb(0xFF, 0x5D, 0x5D, 0x5D));

        void Section(string key, params UIElement[] items)
        {
            var box = new StackPanel { Spacing = 2 };
            box.Children.Add(new TextBlock { Text = S.T(key), FontSize = 11, Foreground = muted });
            foreach (var i in items) box.Children.Add(i);
            credits.Children.Add(box);
        }

        HyperlinkButton Link(string text, string url)
        {
            var b = new HyperlinkButton { Content = text, FontSize = 12, Padding = new Thickness(0, 2, 0, 2) };
            b.Click += (_, _) => Open(url);
            return b;
        }

        TextBlock Plain(string text) => new() { Text = text, FontSize = 12 };

        Section("developedBy", Link("NoCloudware", "https://www.nocloudware.com"));
        Section("thirdParty", Libraries.Select(l => Link(l.Name, l.Url)).ToArray());
        Section("technologies", Plain(".NET 10"), Plain("WinUI 3"), Plain("Inno Setup 6"));
        Section("license", Plain("MIT"));
    }

    void Open(string url)
    {
        try { Discover.OpenPage(url); }
        catch (Exception ex) { Program.Error(ex); }
    }

    async void OnUpdateClick(object sender, RoutedEventArgs e)
    {
        btnUpdate.IsEnabled = false;
        btnUpdate.Content = S.T("checking");
        try
        {
            var found = await Update.FindAsync();
            btnUpdate.IsEnabled = true;
            btnUpdate.Content = S.T("checkUpdates");

            if (found is null) { Program.Msg(S.T("upToDate")); return; }
            if (!Program.Confirm(S.F("confirmUpdate", found.Value.Ver.ToString(2)))) return;

            btnUpdate.Content = S.T("downloading");
            await Update.InstallAsync(found.Value.Url);
            Close(); // the helper takes over: it waits for us, swaps the file and starts it again
        }
        catch (Exception ex)
        {
            btnUpdate.IsEnabled = true;
            btnUpdate.Content = S.T("checkUpdates");
            // The real reason, not "check your internet": a download that failed looks identical
            // from here and the guess sends us hunting for a network problem that did not exist.
            Program.Error(ex);
        }
    }

    void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}