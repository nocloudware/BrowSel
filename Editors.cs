using System.Diagnostics;
using Microsoft.Win32;

// Text editors registered on this machine, the same list Windows shows in "Open with" for .txt.
// The default comes from the operating system user choice; Notepad is assumed when there is none.
static class Editors
{
    internal sealed record Ed(string Id, string Name, string Exe)
    {
        // The ComboBox shows ToString(): without this the raw ProgId is displayed.
        public override string ToString() => Name;
    }

    // "Open with" also keeps a per-user recent list. Windows does not create it until you use it.
    static readonly string[] UserLists =
    {
        @"Software\Classes\.txt\OpenWithList",
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.txt\OpenWithList",
    };

    internal static string DefaultId()
    {
        using var uc = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts\.txt\UserChoice");
        if (uc?.GetValue("ProgId") is string p && p.Length > 0) return p;

        using var cls = Registry.ClassesRoot.OpenSubKey(@".txt");
        return cls?.GetValue(null) as string ?? "";
    }

    // Only editors we can point at a real executable file are listed. Anything else would show up as
    // a bare ProgId (txtfile, vlc.exe, firefox.exe, the Store AppUserModelId...) with no name and no
    // icon: junk the user cannot make sense of, and not something we can show an icon for.
    internal static List<Ed> All()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<Ed>();

        void Add(string? id)
        {
            if (string.IsNullOrWhiteSpace(id) || !seen.Add(id)) return;
            var exe = ExeOf(id);
            if (exe == null || !File.Exists(exe)) return;
            list.Add(new Ed(id, NameOf(id, exe), exe));
        }

        Add(DefaultId());

        // The default above is often a Store app with no command line in the registry, so it never
        // survives the filter. Classic Notepad is the fallback the user asked for.
        if (list.Count == 0) Add("notepad.exe");

        using (var prog = Registry.CurrentUser.OpenSubKey(@"Software\Classes\.txt\OpenWithProgids"))
            if (prog != null)
                foreach (var n in prog.GetValueNames()) Add(n);

        foreach (var path in UserLists)
            using (var k = Registry.CurrentUser.OpenSubKey(path))
                if (k != null)
                    foreach (var n in k.GetValueNames()) Add(k.GetValue(n) as string ?? n);

        if (list.Count == 0) Add("notepad.exe");
        return list;
    }

    static string? ExeOf(string id)
    {
        using var cmd = Registry.ClassesRoot.OpenSubKey(id + @"\shell\open\command");
        return cmd?.GetValue(null) is string c && c.Length > 0 ? FirstToken(c) : null;
    }

    static string NameOf(string id, string exe)
    {
        using var k = Registry.ClassesRoot.OpenSubKey(id);
        if (k?.GetValue("FriendlyAppName") is string f && f.Length > 0) return f;

        var name = Path.GetFileNameWithoutExtension(exe);
        return name.Length > 0 ? name : id;
    }

    static string FirstToken(string command)
    {
        var c = command.Trim();
        if (!c.StartsWith('"')) return c.Split(' ')[0];
        var end = c.IndexOf('"', 1);
        return end > 0 ? c[1..end] : c.Trim('"');
    }

    // The link goes into a real file, opened by the chosen editor's own executable. If the editor
    // cannot be found (or fails to start) the shell's default for .txt is used instead.
    internal static void Open(string id, string text)
    {
        var dir = Path.Combine(Path.GetTempPath(), "BrowSel");
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, $"link-{Guid.NewGuid():N}.txt");
        File.WriteAllText(file, text);

        var ed = All().FirstOrDefault(e => string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));
        if (ed != null)
        {
            try
            {
                var psi = new ProcessStartInfo(ed.Exe) { UseShellExecute = false };
                psi.ArgumentList.Add(file);
                Process.Start(psi)?.Dispose();
                return;
            }
            catch { /* fall through to the default */ }
        }
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true })?.Dispose();
    }

    // One file per click; the editor may still have it open, so only old ones are removed.
    internal static void CleanTemp()
    {
        try
        {
            var dir = Path.Combine(Path.GetTempPath(), "BrowSel");
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.EnumerateFiles(dir, "link-*.txt"))
                if (File.GetCreationTimeUtc(f) < DateTime.UtcNow.AddDays(-1)) File.Delete(f);
        }
        catch { }
    }
}