using System.IO;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace ChatGalvanometer;

public partial class App : Application
{
    [STAThread]
    public static void Main()
    {
        try
        {
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
        }
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (s, args) =>
        {
            WriteCrashLog(args.Exception);
            args.Handled = true;
            Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
            WriteCrashLog((Exception)args.ExceptionObject);

        base.OnStartup(e);
    }

    private static void WriteCrashLog(Exception ex)
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChatGalvanometer");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "crash.log"), $"{DateTime.Now}\n{ex}");
        }
        catch { }
    }
}

