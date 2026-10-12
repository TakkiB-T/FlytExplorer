using Microsoft.UI.Xaml;

namespace FlytExplorer.App;

public partial class App : Application
{
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
        UnhandledException += (_, e) =>
        {
            var msg = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{e.Exception}\n\n";
            try
            {
                var log = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    "FlytExplorer_crash.txt");
                System.IO.File.AppendAllText(log, msg);
            }
            catch { }
            e.Handled = true;
        };
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
