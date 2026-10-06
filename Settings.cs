using Microsoft.Win32;

// Which browsers to show, which branches of the tree were left open, and in what language.
record Settings(string[] Browsers, string[] Expanded, string Lang, string Editor)
{
    const string Key = @"Software\BrowSel\Settings";

    public static Settings Load(IReadOnlyList<Browser> all)
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key);
        var names = k?.GetValue("Browsers") as string;
        var expanded = k?.GetValue("Expanded") as string;
        var lang = k?.GetValue("Lang") as string;
        var editor = k?.GetValue("Editor") as string;
        return new Settings(
            // First run: everything. After that: only the ones still installed and in the list.
            names?.Split('|', StringSplitOptions.RemoveEmptyEntries) ?? all.Select(b => b.Name).ToArray(),
            expanded?.Split('|', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>(),
            lang ?? "",
            editor ?? "");
    }

    public void Save()
    {
        using var k = Registry.CurrentUser.CreateSubKey(Key);
        k.SetValue("Browsers", string.Join('|', Browsers));
        k.SetValue("Expanded", string.Join('|', Expanded));
        k.SetValue("Lang", Lang);
        k.SetValue("Editor", Editor);
    }
}