using System;
using System.IO;
using Avalonia;
using CodeViewer.Services;

namespace CodeViewer;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            LogCrash(e.ExceptionObject as Exception);
        };

        System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            LogCrash(e.Exception);
            e.SetObserved();
        };

        // If an instance of CodeViewer is already running, forward the file arguments and exit
        if (!SingleInstanceService.TryRegisterSingleInstance(args))
        {
            return;
        }

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogCrash(ex);
            throw;
        }
        finally
        {
            SingleInstanceService.Stop();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    public static void LogCrash(Exception? ex)
    {
        if (ex == null) return;
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var dir = Path.Combine(appData, "CodeViewer");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            var crashLog = Path.Combine(dir, "crash.log");
            var content = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}] CRASH:\n{ex}\n----------------------------------------\n";
            File.AppendAllText(crashLog, content);
        }
        catch
        {
            // Best effort logging
        }
    }
}
