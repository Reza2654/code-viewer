using System;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CodeViewer.Configuration;
using CodeViewer.Services;
using CodeViewer.ViewModels;
using CodeViewer.Views;

namespace CodeViewer;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override async void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // 1. Load application configuration
            var config = EnvConfigLoader.LoadConfig();

            // 2. Initialize Core Services
            var fileService = new FileService();
            var languageService = new LanguageService();
            var recentFilesService = new RecentFilesService(config.MaxRecentFiles);
            var dialogService = new DialogService();

            var themeService = new ThemeService();
            var pluginService = new PluginService();

            // 3. Create Main ViewModel
            var mainViewModel = new MainViewModel(
                fileService,
                languageService,
                recentFilesService,
                dialogService,
                config,
                themeService,
                pluginService);

            // 4. Create and show MainWindow
            var mainWindow = new MainWindow
            {
                DataContext = mainViewModel
            };

            dialogService.Initialize(mainWindow);
            desktop.MainWindow = mainWindow;

            // 5. Run async initialization (recent files, command-line arguments)
            await mainViewModel.InitializeAsync(desktop.Args);

            // 6. Listen for incoming file open requests from secondary instances (e.g. File Explorer context menu or CLI)
            SingleInstanceService.SetArgsHandler(files =>
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                {
                    var parsedArgs = CommandLineParser.ParseArguments(files);
                    foreach (var arg in parsedArgs)
                    {
                        if (System.IO.File.Exists(arg.FilePath))
                        {
                            await mainViewModel.OpenFileInternalAsync(arg.FilePath, arg.Line, arg.Column);
                        }
                    }

                    if (mainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
                    {
                        mainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
                    }
                    mainWindow.Activate();
                    mainWindow.Focus();
                });
            });
        }

        base.OnFrameworkInitializationCompleted();
    }
}
