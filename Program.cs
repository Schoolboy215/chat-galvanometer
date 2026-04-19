using System.IO;
using System.Windows;

namespace ChatGalvanometer;

public class Program
{
    [STAThread]
    public static void Main()
    {
        try
        {
            var app = new App();
            app.Run(new MainWindow());
        }
        catch (Exception ex)
        {
            WriteCrashLog(ex);
        }
    }

    internal static void WriteCrashLog(Exception ex)
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
