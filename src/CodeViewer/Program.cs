using System;
using Avalonia;
using CodeViewer.Services;

namespace CodeViewer;

internal sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // If an instance of CodeViewer is already running, forward the file arguments and exit
        if (!SingleInstanceService.TryRegisterSingleInstance(args))
        {
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
