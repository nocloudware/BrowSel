using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Win32;

static class Program
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBoxW(IntPtr h, string text, string caption, uint type);
    const uint MbIconInformation = 0x40, MbIconError = 0x10, MbYesNo = 0x4;
    const int IdYes = 6;

    // The window that owns the message boxes. Without an owner they open behind the app and look like
    // nothing happened.
    internal static IntPtr Owner;

    internal static void Msg(string text, string title = "BrowSel") =>
        MessageBoxW(Owner, text, title, MbIconInformation);

    // Yes/no question. A message box rather than a ContentDialog: the update button lives inside
    // the settings dialog, and Windows refuses to open a second dialog on top of an open one.
    internal static bool Confirm(string text) =>
        MessageBoxW(Owner, text, "BrowSel", MbYesNo) == IdYes;

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

        // Windows may pass "BrowSelURL:<uri>", a plain URL, a file: URI, or a local path.
        var target = Target.FromArgument(args[0]);
        if (target is null) return;

        Url = target;
        Update.CleanTemp();
        Editors.CleanTemp();
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(p =>
        {
            var ctx = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(ctx);
            _ = new BrowSel.App();
        });
    }

    internal static string Url = "";

    // Web page documents BrowSel can open. Keep in sync with installer\BrowSel.iss.
    static readonly (string Ext, string Prog)[] FileTypes =
    {
        (".htm", "BrowSelHTML"),
        (".html", "BrowSelHTML"),
        (".shtml", "BrowSelHTML"),
        (".xht", "BrowSelHTML"),
        (".xhtml", "BrowSelHTML"),
        (".mht", "BrowSelMHTML"),
        (".mhtml", "BrowSelMHTML"),
    };

    static void RegisterProgId(string progId, string defaultValue, string exe, bool urlProtocol = false)
    {
        using var k = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}");
        k.SetValue("", defaultValue);
        if (urlProtocol) k.SetValue("URL Protocol", "");
        using (var i = k.CreateSubKey("DefaultIcon")) i.SetValue("", $"\"{exe}\",0");
        using var c = k.CreateSubKey(@"shell\open\command");
        c.SetValue("", $"\"{exe}\" \"%1\"");
    }

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

        RegisterProgId(prog, "URL:BrowSelURL", exe, urlProtocol: true);
        RegisterProgId("BrowSelHTML", "BrowSel HTML Document", exe);
        RegisterProgId("BrowSelMHTML", "BrowSel MHTML Document", exe);
        using (var k = Registry.CurrentUser.CreateSubKey($@"{app}\Capabilities"))
        {
            k.SetValue("ApplicationName", "BrowSel");
            k.SetValue("ApplicationDescription", S.T("appDescription"));
            k.SetValue("AppPath", exe);
            using (var f = k.CreateSubKey("FileAssociations"))
                foreach (var (ext, progId) in FileTypes)
                    f.SetValue(ext, progId);
            using var u = k.CreateSubKey("URLAssociations");
            u.SetValue("http", prog);
            u.SetValue("https", prog);
        }
        foreach (var (ext, progId) in FileTypes)
        {
            using var k = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}\OpenWithProgids");
            k.SetValue(progId, "", RegistryValueKind.String);
        }
        using (var k = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            k.SetValue("BrowSel", $@"{app}\Capabilities");

        Process.Start(new ProcessStartInfo("ms-settings:defaultapps?registeredAppUser=BrowSel") { UseShellExecute = true })?.Dispose();
        Msg(S.T("registered"));
    }

    static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\BrowSelURL", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\BrowSelHTML", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\BrowSelMHTML", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\BrowSel", false);

        foreach (var (ext, progId) in FileTypes)
            using (var k = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{ext}\OpenWithProgids", true))
                k?.DeleteValue(progId, false);

        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
            k?.DeleteValue("BrowSel", false);
        Msg(S.T("unregistered"));
    }
}
