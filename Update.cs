using System.Diagnostics;
using System.Text.Json;

// Checks the project's GitHub releases and, when there is a newer one, downloads it and swaps it in.
// ponytail: no installer, no auto-updater library. The swap is handed to a hidden PowerShell because a
// running exe cannot overwrite itself; if the download fails nothing on disk changes.
static class Update
{
    const string Api = "https://api.github.com/repos/nocloudware/BrowSel/releases/latest";

    internal static Version Current
    {
        get
        {
            var v = typeof(Update).Assembly.GetName().Version ?? new Version(0, 0);
            return new Version(v.Major, v.Minor, Math.Max(v.Build, 0)); // drop the revision
        }
    }

    // Newest release newer than the running one, or null when there is none (or the call failed).
    internal static async Task<(Version Ver, string Url)?> FindAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("BrowSel");

            using var json = JsonDocument.Parse(await http.GetStringAsync(Api));
            var ver = Parse(json.RootElement.GetProperty("tag_name").GetString());
            if (ver == null || ver <= Current) return null;

            foreach (var a in json.RootElement.GetProperty("assets").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                    name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    return (ver, a.GetProperty("browser_download_url").GetString()!);
            }

            return null;
        }
        catch { return null; } // offline, rate limited, malformed json: treat as "nothing to offer"
    }

    // The release asset is the Inno Setup installer. It is downloaded next to us and run silently once
    // we exit: it puts the new files in place over the current install and registers itself.
    internal static async Task InstallAsync(string url)
    {
        var exe = Environment.ProcessPath ?? throw new InvalidOperationException("no exe path");
        var folder = AppContext.BaseDirectory;
        var setup = Path.Combine(folder, "browsel-update.exe");
        var script = Path.Combine(folder, "browsel-update.ps1");

        using (var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) })
        using (var src = await http.GetStreamAsync(url))
        using (var dst = File.Create(setup))
            await src.CopyToAsync(dst);

        File.WriteAllText(script, $@"
$setup = '{setup}'
$dir = '{folder}'
$me = '{script}'
$exe = '{exe}'
while (Get-Process -Id {Environment.ProcessId} -EA SilentlyContinue) {{ Start-Sleep -Milliseconds 300 }}
Start-Process $setup -ArgumentList '/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/SP-' -Wait
Start-Process $exe
Remove-Item -LiteralPath $setup, $me -Force -EA SilentlyContinue
");

        Process.Start(new ProcessStartInfo("powershell.exe",
            $"-NoProfile -WindowStyle Hidden -ExecutionPolicy Bypass -File \"{script}\"")
        { UseShellExecute = false })?.Dispose();
    }

    static Version? Parse(string? tag)
    {
        if (tag == null) return null;
        var t = tag.TrimStart('v', 'V');
        var parts = t.Split('.');
        var nums = new List<int>();
        foreach (var p in parts.Take(3))
        {
            var digits = new string(p.TakeWhile(char.IsAsciiDigit).ToArray());
            if (!int.TryParse(digits, out var n)) break;
            nums.Add(n);
        }
        return nums.Count == 0 ? null : new Version(nums[0], nums.Count > 1 ? nums[1] : 0, nums.Count > 2 ? nums[2] : 0);
    }
}
