using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Win32;

static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr h, string text, string caption, uint type);
    const uint MbIconInformation = 0x40, MbIconError = 0x10;

    // The window that owns the message boxes. Without an owner they open behind the app and look like
// nothing happened.
internal static IntPtr Owner;

    internal static void Msg(string text, string title = "BrowSel") =>
        MessageBoxW(Owner, text, title, MbIconInformation);

    internal static void Error(Exception? ex) =>
        MessageBoxW(Owner,
            S.T("crash") + "\n\n" + (ex?.ToString() ?? S.T("noDetail")),
            "BrowSel", MbIconError);

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Msg(S.T("noArgs"));
            return;
        }
        if (args[0] == "--register") { Register(); return; }
        if (args[0] == "--unregister") { Unregister(); return; }

        // Windows may pass the whole URI ("BrowSelURL:https://...") or just the URL
        var url = args[0];
        var colon = url.IndexOf(':');
        if (colon >= 0 && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = url[(colon + 1)..];

        // http/https only: stops an argument from being read as a browser option
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;

        Url = url;
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var ctx = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(ctx);
            _ = new BrowSel.App();
        });
    }

    internal static string Url = "";

    // ---------- Protocol registration / unregistration ----------

    static void Register()
    {
        var exe = Environment.ProcessPath!;
        if (Path.GetFileName(exe).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var apphost = Path.Combine(AppContext.BaseDirectory, "BrowSel.exe");
            if (!File.Exists(apphost))
            {
                Msg(S.T("dotnetHint"));
                return;
            }
            exe = apphost;
        }
        const string prog = "BrowSelURL", app = @"Software\BrowSel";

        using (var k = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{prog}"))
        {
            k.SetValue("", "BrowSel URL");
            k.SetValue("URL Protocol", "");
            using var c = k.CreateSubKey(@"shell\open\command");
            c.SetValue("", $"\"{exe}\" \"%1\"");
        }
        using (var k = Registry.CurrentUser.CreateSubKey($@"{app}\Capabilities"))
        {
            k.SetValue("ApplicationName", "BrowSel");
            k.SetValue("ApplicationDescription", S.T("appDescription"));
            using var u = k.CreateSubKey("URLAssociations");
            u.SetValue("http", prog);
            u.SetValue("https", prog);
        }
        using (var k = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            k.SetValue("BrowSel", $@"{app}\Capabilities");

        Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true })?.Dispose();
        Msg(S.T("registered"));
    }

    static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\BrowSelURL", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\BrowSel", false);
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
            k?.DeleteValue("BrowSel", false);
        Msg(S.T("unregistered"));
    }
}
