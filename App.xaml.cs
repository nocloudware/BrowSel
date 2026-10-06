using Microsoft.UI.Xaml;

namespace BrowSel;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        // A crash used to close the app silently: the user only saw the window disappear.
        UnhandledException += (_, e) => { e.Handled = true; Program.Error(e.Exception); };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        new MainWindow().Activate();
    }
}
