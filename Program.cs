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

    internal static void Msg(string text, string title = "BrowSel") =>
        MessageBoxW(IntPtr.Zero, text, title, MbIconInformation);

    internal static void Error(Exception? ex) =>
        MessageBoxW(IntPtr.Zero,
            "BrowSel se cerro por un error inesperado.\n\n" + (ex?.ToString() ?? "sin detalle"),
            "BrowSel", MbIconError);

    [STAThread]
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            Msg("Ejecuta: BrowSel.exe --register (o --unregister para quitarlo)\nLuego elige 'BrowSel' como navegador predeterminado.");
            return;
        }
        if (args[0] == "--register") { Register(); return; }
        if (args[0] == "--unregister") { Unregister(); return; }

        // Windows puede pasar la URI completa ("BrowSelURL:https://...") o solo la URL
        var url = args[0];
        var colon = url.IndexOf(':');
        if (colon >= 0 && !url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            url = url[(colon + 1)..];

        // Solo http/https: evita que un argumento se interprete como opcion del navegador
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

    // ---------- Registro como protocolo / desregistro ----------

    static void Register()
    {
        var exe = Environment.ProcessPath!;
        if (Path.GetFileName(exe).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
        {
            var apphost = Path.Combine(AppContext.BaseDirectory, "BrowSel.exe");
            if (!File.Exists(apphost))
            {
                Msg("Compila con 'dotnet build' y ejecuta BrowSel.exe directamente.");
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
            k.SetValue("ApplicationDescription", "Selector de perfiles para enlaces");
            using var u = k.CreateSubKey("URLAssociations");
            u.SetValue("http", prog);
            u.SetValue("https", prog);
        }
        using (var k = Registry.CurrentUser.CreateSubKey(@"Software\RegisteredApplications"))
            k.SetValue("BrowSel", $@"{app}\Capabilities");

        Process.Start(new ProcessStartInfo("ms-settings:defaultapps") { UseShellExecute = true })?.Dispose();
        Msg("BrowSel quedo registrado, pero todavia NO es tu navegador predeterminado. " +
            "Windows exige confirmarlo a mano: en la ventana de Configuracion que se abrio, " +
            "busca BrowSel y asignalo a HTTP y HTTPS.");
    }

    static void Unregister()
    {
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\Classes\BrowSelURL", false);
        Registry.CurrentUser.DeleteSubKeyTree(@"Software\BrowSel", false);
        using (var k = Registry.CurrentUser.OpenSubKey(@"Software\RegisteredApplications", true))
            k?.DeleteValue("BrowSel", false);
        Msg("BrowSel fue desregistrado. Si seguia como predeterminado, " +
            "Windows te pedira elegir otro navegador al abrir un enlace.");
    }
}
