using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

// Checks the project's GitHub releases and, when there is a newer one, downloads the Inno Setup
// installer, verifies its SHA256 and runs it silently. Inno closes this app and replaces the files.
// No hash published = no update: a failed check is safer than running an unchecked exe.
static class Update
{
    const string Api = "https://api.github.com/repos/nocloudware/BrowSel/releases/latest";
    const string SetupPrefix = "BrowSelSetup";

    internal sealed record Release(Version Ver, string Url, string Sha256);

    static readonly HttpClient Http = NewClient();
    static HttpClient NewClient()
    {
        var h = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        h.DefaultRequestHeaders.UserAgent.ParseAdd("BrowSel");
        return h;
    }

    internal static Version Current
    {
        get
        {
            var v = typeof(Update).Assembly.GetName().Version ?? new Version(0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0)); // drop the revision
        }
    }

    // Newest release newer than the running one, or null (none, offline, no checksum, malformed).
    internal static async Task<Release?> FindAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var json = JsonDocument.Parse(await Http.GetStringAsync(Api, cts.Token));
            var root = json.RootElement;

            var ver = UpdateParsing.ParseVersion(root.GetProperty("tag_name").GetString());
            if (ver == null || ver <= Current) return null;

            string? url = null, hash = null, sidecar = null;
            foreach (var a in root.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                var dl = a.GetProperty("browser_download_url").GetString();
                if (!name.StartsWith(SetupPrefix, StringComparison.OrdinalIgnoreCase)) continue;

                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    url = dl;
                    if (a.TryGetProperty("digest", out var d) && d.GetString() is { } s &&
                        s.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                        hash = UpdateParsing.ParseHash(s[7..]);
                }
                else if (name.EndsWith(".exe.sha256", StringComparison.OrdinalIgnoreCase)) sidecar = dl;
            }

            if (url == null) return null;
            if (hash == null && sidecar != null)
                hash = UpdateParsing.ParseHash(await Http.GetStringAsync(sidecar, cts.Token));
            return hash == null ? null : new Release(ver, url, hash);
        }
        catch { return null; } // offline, rate limited, malformed json: nothing to offer
    }

    // Downloads to a unique temp folder, verifies the hash, starts the installer. The caller must
    // exit the app right after: Inno would otherwise have to close it.
    internal static async Task InstallAsync(Release r)
    {
        var dir = Path.Combine(Path.GetTempPath(), "BrowSel-update-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var setup = Path.Combine(dir, "BrowSelSetup.exe");

        try
        {
            using (var src = await Http.GetStreamAsync(r.Url))
            using (var dst = File.Create(setup))
                await src.CopyToAsync(dst);

            string actual;
            using (var f = File.OpenRead(setup))
                actual = Convert.ToHexString(await SHA256.HashDataAsync(f));
            if (!actual.Equals(r.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("The downloaded installer does not match its published SHA256. Update cancelled.");

            var psi = new ProcessStartInfo(setup) { UseShellExecute = false };
            foreach (var a in new[] { "/SILENT", "/SUPPRESSMSGBOXES", "/NORESTART", "/CLOSEAPPLICATIONS", "/SP-" })
                psi.ArgumentList.Add(a);
            Process.Start(psi)?.Dispose();
        }
        catch
        {
            try { Directory.Delete(dir, true); } catch { }
            throw;
        }
    }

    // Installer folders left by earlier updates (the installer cannot delete itself while running).
    internal static void CleanTemp()
    {
        try
        {
            foreach (var d in Directory.EnumerateDirectories(Path.GetTempPath(), "BrowSel-update-*"))
                if (Directory.GetCreationTimeUtc(d) < DateTime.UtcNow.AddDays(-1)) Directory.Delete(d, true);
        }
        catch { }
    }
}
