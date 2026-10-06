using Microsoft.Win32;

// Qué navegadores mostrar y qué ramas del árbol quedaron abiertas.
record Settings(string[] Browsers, string[] Expanded)
{
    const string Key = @"Software\BrowSel\Settings";

    public static Settings Load(IReadOnlyList<Browser> all)
    {
        using var k = Registry.CurrentUser.OpenSubKey(Key);
        var names = k?.GetValue("Browsers") as string;
        var expanded = k?.GetValue("Expanded") as string;
        return new Settings(
            // Primera vez: todos. Después: solo los que siguen instalados y existen en la lista.
            names?.Split('|', StringSplitOptions.RemoveEmptyEntries) ?? all.Select(b => b.Name).ToArray(),
            expanded?.Split('|', StringSplitOptions.RemoveEmptyEntries) ?? Array.Empty<string>());
    }

    public void Save()
    {
        using var k = Registry.CurrentUser.CreateSubKey(Key);
        k.SetValue("Browsers", string.Join('|', Browsers));
        k.SetValue("Expanded", string.Join('|', Expanded));
    }

    }