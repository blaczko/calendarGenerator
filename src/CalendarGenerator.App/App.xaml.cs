using System.Windows;
using CalendarGenerator.App.Services;
using QuestPDF.Infrastructure;

namespace CalendarGenerator.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        base.OnStartup(e);

        if (e.Args.Any(a => a.Equals("--smoke-pdf", StringComparison.OrdinalIgnoreCase)))
        {
            ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var code = await SmokeTestRunner.RunAsync();
            Shutdown(code);
            return;
        }

        var window = new MainWindow();
        MainWindow = window;
        window.Show();
    }
}
