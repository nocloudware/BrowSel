using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Win32;

enum Kind { Chromium, Firefox }
// Data = path to "Local State" (Chromium) or to "profiles.ini" (Firefox)
record Browser(string Name, string Product, Kind Kind, string ExeName, string[] ExePaths, string Data);
record Proc(string Name, uint Pid, string Cmd);
record Entry(Browser B, string Exe, string Dir, string Label, bool Open, string Why);

// Browser discovery, open-profile detection and launching.
static class Discover
{
    static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    static readonly string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    static readonly string PF = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
    static readonly string PF86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

    static Browser Chromium(string name, string product, string rel, string exe) => new(name, product, Kind.Chromium, exe,
        new[] { Path.Combine(PF, rel, "Application", exe), Path.Combine(PF86, rel, "Application", exe),
                Path.Combine(Local, rel, "Application", exe) },
        Path.Combine(Local, rel, "User Data", "Local State"));

    public static readonly Browser[] Browsers =
    {
        Chromium("Brave", "Brave", @"BraveSoftware\Brave-Browser", "brave.exe"),
        Chromium("Chrome", "Google Chrome", @"Google\Chrome", "chrome.exe"),
        Chromium("Edge", "Microsoft Edge", @"Microsoft\Edge", "msedge.exe"),
        new("Opera", "Opera", Kind.Chromium, "opera.exe",
            new[] { Path.Combine(Local, @"Programs\Opera\opera.exe"), Path.Combine(PF, @"Opera\opera.exe") },
            Path.Combine(Roaming, @"Opera Software\Opera Stable\Local State")),
        new("Firefox", "Mozilla Firefox", Kind.Firefox, "firefox.exe",
            new[] { Path.Combine(PF, @"Mozilla Firefox\firefox.exe"), Path.Combine(PF86, @"Mozilla Firefox\firefox.exe") },
            Path.Combine(Roaming, @"Mozilla\Firefox\profiles.ini")),
    };

    // One single WMI query for every browser, shared by the whole picker
    static List<Proc>? _snap;
    static List<Proc> Snapshot()
    {
        if (_snap != null) return _snap;
        var where = string.Join(" OR ", Browsers.Select(b => $"Name='{b.ExeName}'"));
        var list = new List<Proc>();
        using var s = new ManagementObjectSearcher($"SELECT Name, ProcessId, CommandLine FROM Win32_Process WHERE {where}");
        foreach (ManagementObject o in s.Get())
            using (o) list.Add(new Proc((string)o["Name"], (uint)o["ProcessId"], o["CommandLine"] as string ?? ""));
        return _snap = list;
    }

    public static string? FindExe(Browser b)
    {
        // Known install paths first; the registry only as a fallback (installs elsewhere)
        var known = b.ExePaths.FirstOrDefault(File.Exists);
        if (known != null) return known;
        foreach (var hive in new[] { "HKEY_LOCAL_MACHINE", "HKEY_CURRENT_USER" })
            if (Registry.GetValue($@"{hive}\SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\{b.ExeName}", "", null) is string p
                && File.Exists(p)) return p;
        return null;
    }

    public static List<Entry> Load(Browser b)
    {
        var exe = FindExe(b);
        if (exe == null || !File.Exists(b.Data)) return new();
        return b.Kind == Kind.Firefox ? LoadFirefox(b, exe) : LoadChromium(b, exe);
    }

    static List<Entry> LoadChromium(Browser b, string exe)
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(b.Data));
        if (!doc.RootElement.TryGetProperty("profile", out var profile) ||
            !profile.TryGetProperty("info_cache", out var cache) || cache.ValueKind != JsonValueKind.Object)
        {
            // No profile list (e.g. Opera): a single entry
            bool run = Snapshot().Any(p => p.Name.Equals(b.ExeName, StringComparison.OrdinalIgnoreCase) && !p.Cmd.Contains("--type="));
            return new() { new Entry(b, exe, "", S.F("singleProfile", b.Name), run, run ? S.T("whyProcess") : "") };
        }

        var names = cache.EnumerateObject().ToDictionary(
            p => p.Name,
            p => p.Value.TryGetProperty("name", out var n) ? n.GetString() ?? p.Name : p.Name);
        var open = ChromiumOpen(b, exe, profile, names);

        return names.Select(kv =>
        {
            open.TryGetValue(kv.Key, out var why);
            return new Entry(b, exe, kv.Key, $"{kv.Value}  ({b.Name})", why != null, why == null ? "" : string.Join(", ", why));
        }).ToList();
    }

    static List<Entry> LoadFirefox(Browser b, string exe)
    {
        var baseDir = Path.GetDirectoryName(b.Data)!;
        var result = new List<Entry>();
        var cur = new Dictionary<string, string>();
        string sec = "";

        // Firefox keeps the names the user gave each profile in a per-group SQLite database, and
        // leaves profiles.ini holding nothing but folder names. So the database goes first and its
        // name wins: it is the same name the profile manager shows.
        var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, label) in GroupNames(Path.Combine(baseDir, "Profile Groups")))
            named[Path.GetFullPath(Path.Combine(baseDir, path))] = label;

        void Add(string name, string full)
        {
            full = Path.GetFullPath(full);
            if (result.Any(e => string.Equals(e.Dir, full, StringComparison.OrdinalIgnoreCase))) return;
            var real = named.TryGetValue(full, out var label) ? label : RealName(full);
            // ponytail: if Firefox changes how parent.lock is taken, this stops seeing the open profile.
            bool open = IsLocked(Path.Combine(full, "parent.lock"));
            result.Add(new Entry(b, exe, full, $"{(real == "" ? name : real)}  (Firefox)", open, open ? S.T("whyLock") : ""));
        }

        foreach (var (dir, label) in named) Add(label, dir);

        void Flush()
        {
            if (!sec.StartsWith("[Profile", StringComparison.OrdinalIgnoreCase)) return;
            if (!cur.TryGetValue("Name", out var name) || !cur.TryGetValue("Path", out var path)) return;
            Add(name, cur.GetValueOrDefault("IsRelative") == "1" ? Path.Combine(baseDir, path.Replace('/', '\\')) : path);
        }

        foreach (var raw in File.ReadAllLines(b.Data))
        {
            var l = raw.Trim();
            if (l.StartsWith("[")) { Flush(); sec = l; cur = new(); continue; }
            var i = l.IndexOf('=');
            if (i > 0) cur[l[..i]] = l[(i + 1)..];
        }
        Flush();

        // Firefox can drop a profile from profiles.ini when it rewrites the file, but it leaves the
        // folder behind with everything still in it. Reading only the ini hid those, so scan the
        // profile folder too: a real profile has a prefs.js.
        var profilesDir = Path.Combine(baseDir, "Profiles");
        if (Directory.Exists(profilesDir))
            foreach (var dir in Directory.EnumerateDirectories(profilesDir))
                if (File.Exists(Path.Combine(dir, "prefs.js")))
                    Add(Path.GetFileName(dir).Split('.').Last(), dir);

        return result;
    }

    // The names Firefox shows in its own profile manager. Nothing else on disk carries them:
    // profiles.ini has folder names, and prefs.js only has one for the profiles that happened to
    // get a Start menu shortcut. Only used when the profile database has nothing to say.
    static string RealName(string profileDir)
    {
        var prefs = Path.Combine(profileDir, "prefs.js");
        if (!File.Exists(prefs)) return "";
        var m = Regex.Match(File.ReadAllText(prefs),
            "^user_pref\\(\"browser\\.profiles\\.shortcutFileName\", \"([^\"]+)\"\\);", RegexOptions.Multiline);
        return m.Success ? Path.GetFileNameWithoutExtension(m.Groups[1].Value) : "";
    }

    static List<(string Path, string Name)> GroupNames(string groupsDir)
    {
        var rows = new List<(string, string)>();
        if (!Directory.Exists(groupsDir)) return rows;
        foreach (var db in Directory.EnumerateFiles(groupsDir, "*.sqlite"))
        {
            // A copy, so a running Firefox cannot block the read and a write ahead log left
            // pending by a crash still gets replayed against it.
            var tmp = Path.Combine(Path.GetTempPath(), "BrowSel-" + Path.GetFileName(db));
            try
            {
                File.Copy(db, tmp, true);
                if (File.Exists(db + "-wal")) File.Copy(db + "-wal", tmp + "-wal", true);
                rows.AddRange(Sqlite.Select(tmp, "SELECT path, name FROM Profiles"));
            }
            catch (Exception) { }
            finally { File.Delete(tmp); File.Delete(tmp + "-wal"); }
        }
        return rows;
    }

    // The SQLite that ships with Windows, so reading a profile database needs no package.
    static class Sqlite
    {
        const int Row = 100, OpenReadWrite = 0x2, OpenCreate = 0x4;

        [DllImport("winsqlite3.dll")] static extern int sqlite3_open_v2(byte[] f, out IntPtr db, int flags, IntPtr vfs);
        [DllImport("winsqlite3.dll")] static extern int sqlite3_prepare_v2(IntPtr db, byte[] sql, int n, out IntPtr stmt, out IntPtr rest);
        [DllImport("winsqlite3.dll")] static extern int sqlite3_step(IntPtr stmt);
        [DllImport("winsqlite3.dll")] static extern int sqlite3_finalize(IntPtr stmt);
        [DllImport("winsqlite3.dll")] static extern int sqlite3_close(IntPtr db);
        [DllImport("winsqlite3.dll")] static extern IntPtr sqlite3_column_text(IntPtr stmt, int col);

        static byte[] Utf8(string s) { var b = Encoding.UTF8.GetBytes(s); var r = new byte[b.Length + 1]; b.CopyTo(r, 0); return r; }
        static string? Text(IntPtr stmt, int col)
        {
            var p = sqlite3_column_text(stmt, col);
            return p == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(p);
        }

        public static List<(string A, string B)> Select(string file, string sql)
        {
            var rows = new List<(string, string)>();
            if (sqlite3_open_v2(Utf8(file), out var db, OpenReadWrite | OpenCreate, IntPtr.Zero) != 0) return rows;
            try
            {
                if (sqlite3_prepare_v2(db, Utf8(sql), -1, out var st, out _) != 0) return rows;
                try
                {
                    while (sqlite3_step(st) == Row)
                    {
                        var a = Text(st, 0); var b = Text(st, 1);
                        if (a != null && b != null) rows.Add((a, b));
                    }
                }
                finally { sqlite3_finalize(st); }
            }
            finally { sqlite3_close(db); }
            return rows;
        }
    }

    // ---------- Open-profile detection (Chromium) ----------

    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder sb, int n);
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    delegate bool EnumProc(IntPtr h, IntPtr l);

    static readonly string[] LockProbes =
    {
        @"Sync Data\LevelDB\LOCK", @"Local Storage\leveldb\LOCK", @"Session Storage\LOCK", @"shared_proto_db\LOCK"
    };

    static Dictionary<string, List<string>> ChromiumOpen(Browser b, string exe, JsonElement profile,
        Dictionary<string, string> names)
    {
        var res = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        void Add(string dir, string why)
        {
            if (!res.TryGetValue(dir, out var l)) res[dir] = l = new();
            if (!l.Contains(why)) l.Add(why);
        }

        // 1: command line of the main processes (shared snapshot)
        var mainPids = new HashSet<uint>();
        foreach (var p in Snapshot())
        {
            if (!p.Name.Equals(Path.GetFileName(exe), StringComparison.OrdinalIgnoreCase) || p.Cmd.Contains("--type=")) continue;
            mainPids.Add(p.Pid);
            var m = Regex.Match(p.Cmd, "--profile-directory=(?:\"([^\"]+)\"|(\\S+))");
            if (m.Success) Add(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value, S.T("whyCmdline"));
        }
        if (mainPids.Count == 0) return res; // browser closed

        // 2: window titles "... - Product - Profile" or "... - Profile - Product"
        EnumWindows((h, _) =>
        {
            if (!IsWindowVisible(h)) return true;
            GetWindowThreadProcessId(h, out var pid);
            if (!mainPids.Contains(pid)) return true;
            var cls = new StringBuilder(64); GetClassName(h, cls, 64);
            if (cls.ToString() != "Chrome_WidgetWin_1") return true;
            var t = new StringBuilder(512); GetWindowText(h, t, 512);
            var title = t.ToString();
            foreach (var kv in names)
                if (title.EndsWith($" - {b.Product} - {kv.Value}", StringComparison.OrdinalIgnoreCase) ||
                    title.EndsWith($" - {kv.Value} - {b.Product}", StringComparison.OrdinalIgnoreCase))
                    Add(kv.Key, S.T("whyWindow"));
            return true;
        }, IntPtr.Zero);

        // 3: files the browser has locked
        var root = Path.GetDirectoryName(b.Data)!;
        foreach (var dir in names.Keys)
            foreach (var probe in LockProbes)
                if (IsLocked(Path.Combine(root, dir, probe))) { Add(dir, S.T("whyLock")); break; }

        // 4 (fallback only): last_active_profiles, only if no other signal found anything
        if (res.Count == 0 && profile.TryGetProperty("last_active_profiles", out var last))
            foreach (var x in last.EnumerateArray()) Add(x.GetString()!, S.T("whyLastActive"));

        return res;
    }

    static bool IsLocked(string path)
    {
        if (!File.Exists(path)) return false;
        try { using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None); return false; }
        catch (IOException) { return true; }
        catch { return false; }
    }

    // The app's own links, opened in a browser directly instead of through the shell. The shell
    // sends https back to whoever handles it, and when that is BrowSel the user gets a second
    // picker stacked on the first one instead of the page. Same exe lookup as the picker, so the
    // link opens in one of the browsers already on screen.
    public static void OpenPage(string url)
    {
        foreach (var b in Browsers)
        {
            var exe = FindExe(b);
            if (exe == null) continue;
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, ArgumentList = { url } })?.Dispose();
            return;
        }
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
    }

    public static void Launch(Entry e, string url)
    {
        var psi = new ProcessStartInfo(e.Exe) { UseShellExecute = false };
        if (e.B.Kind == Kind.Firefox)
        {
            // Dir is the full profile path: --profile opens a profile Firefox has dropped from
            // profiles.ini too, -P only knows the ones the ini still lists.
            psi.ArgumentList.Add("--profile"); psi.ArgumentList.Add(e.Dir);
            // Profile already open: reuse its instance. Closed: a new instance, no clash with other profiles.
            psi.ArgumentList.Add(e.Open ? "-new-tab" : "-no-remote");
            psi.ArgumentList.Add(url);
        }
        else
        {
            if (e.Dir != "") psi.ArgumentList.Add($"--profile-directory={e.Dir}");
            psi.ArgumentList.Add(url);
        }
        Process.Start(psi)?.Dispose();
    }
}
