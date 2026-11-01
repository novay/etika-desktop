using Microsoft.UI.Xaml;

namespace Etika.Desktop;

public partial class App : Application
{
    private Window? _window;
    public App()
    {
        UnhandledException += (_, args) => StartupDiagnostics.Write("UI", args.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception error) StartupDiagnostics.Write("Process", error);
        };
        try { InitializeComponent(); }
        catch (Exception error)
        {
            StartupDiagnostics.Write("Application.InitializeComponent", error);
            throw;
        }
    }
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        try
        {
            _window = new MainWindow();
            _window.Activate();
        }
        catch (Exception error)
        {
            StartupDiagnostics.Write("MainWindow", error);
            throw;
        }
    }
}
