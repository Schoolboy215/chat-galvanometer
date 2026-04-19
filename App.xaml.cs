using System.IO;
using System.Windows;
using System.Windows.Threading;
using Application = System.Windows.Application;

namespace ChatGalvanometer;

public partial class App : Application
{
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

    private static void WriteCrashLog(Exception ex) => Program.WriteCrashLog(ex);
}

