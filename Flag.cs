using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;

// Flags as images. Windows does NOT draw flag emoji: it shows them as the pair of
// letters ("ES", "GB"), so the PNGs are downloaded once and read from the app folder.
static class Flag
{
    // codigo de idioma -> codigo de pais
    static readonly Dictionary<string, string> Cc = new()
    {
        ["en"] = "gb", ["es"] = "es", ["fr"] = "fr", ["ru"] = "ru", ["zh"] = "cn",
        ["ja"] = "jp", ["pt"] = "pt", ["hi"] = "in", ["ar"] = "sa", ["bn"] = "bd",
    };

    internal static UIElement Of(string code)
    {
        if (!Cc.TryGetValue(code, out var cc)) cc = "gb";
        return new Image
        {
            Source = new BitmapImage(new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", cc + ".png"))),
            Width = 26,
            Height = 17,
            // The images are not all the same shape (the English one is 2:1, the rest 3:2):
            // Uniform keeps them, UniformToFill would distort them.
            Stretch = Stretch.Uniform,
            VerticalAlignment = VerticalAlignment.Center,
        };
    }
}