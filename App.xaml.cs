using Microsoft.UI.Xaml;

namespace BrowSel;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        // Antes un fallo cerraba la app sin decir nada: el usuario solo veia la ventana desaparecer.
        UnhandledException += (_, e) => { e.Handled = true; Program.Error(e.Exception); };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        new MainWindow().Activate();
    }
}
