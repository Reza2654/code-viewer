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
            try
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
                var integrationsUpdateService = new IntegrationsUpdateService();
                var appUpdateService = new AppUpdateService();
                var recentWorkspacesService = new RecentWorkspacesService();

                // 3. Create Main ViewModel
                var mainViewModel = new MainViewModel(
                    fileService,
                    languageService,
                    recentFilesService,
                    dialogService,
                    config,
                    themeService,
                    pluginService,
                    integrationsUpdateService: integrationsUpdateService,
                    appUpdateService: appUpdateService,
                    recentWorkspacesService: recentWorkspacesService);

                // 4. Create MainWindow
                var mainWindow = new MainWindow
                {
                    DataContext = mainViewModel
                };

                dialogService.Initialize(mainWindow);
                desktop.MainWindow = mainWindow;

                // Support startup pre-warming / background mode for instantaneous sub-10ms launching
                var isBackground = desktop.Args != null && System.Linq.Enumerable.Any(desktop.Args, a => string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase));
                desktop.ShutdownMode = isBackground
                    ? Avalonia.Controls.ShutdownMode.OnExplicitShutdown
                    : Avalonia.Controls.ShutdownMode.OnMainWindowClose;

                if (!isBackground)
                {
                    mainWindow.Show();
                }

                desktop.Exit += (_, _) => SingleInstanceService.Stop();

                // Setup System Tray Icon for fast background management & status
                try
                {
                    var trayIcon = new Avalonia.Controls.TrayIcon
                    {
                        ToolTipText = "Code Viewer",
                        IsVisible = true
                    };

                    try
                    {
                        trayIcon.Icon = new Avalonia.Controls.WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://CodeViewer/Assets/icon.ico")));
                    }
                    catch
                    {
                        // Fallback if icon asset stream cannot be resolved
                    }

                    var menu = new Avalonia.Controls.NativeMenu();

                    var openItem = new Avalonia.Controls.NativeMenuItem("Open Code Viewer");
                    openItem.Click += (_, _) => RestoreAndFocusWindow(mainWindow);
                    menu.Add(openItem);

                    var fontItem = new Avalonia.Controls.NativeMenuItem("Toggle Coding Font");
                    fontItem.Click += (_, _) => _ = mainViewModel.ToggleCodingFontCommand.ExecuteAsync(null);
                    menu.Add(fontItem);

                    var updateItem = new Avalonia.Controls.NativeMenuItem("Check for Updates...");
                    updateItem.Click += (_, _) =>
                    {
                        RestoreAndFocusWindow(mainWindow);
                        _ = mainViewModel.CheckForUpdatesCommand.ExecuteAsync(null);
                    };
                    menu.Add(updateItem);

                    menu.Add(new Avalonia.Controls.NativeMenuItemSeparator());

                    var exitItem = new Avalonia.Controls.NativeMenuItem("Exit");
                    exitItem.Click += (_, _) =>
                    {
                        mainWindow.ExplicitExit();
                        desktop.Shutdown();
                    };
                    menu.Add(exitItem);

                    trayIcon.Menu = menu;
                    trayIcon.Clicked += (_, _) => RestoreAndFocusWindow(mainWindow);

                    var trayIcons = new Avalonia.Controls.TrayIcons { trayIcon };
                    Avalonia.Controls.TrayIcon.SetIcons(this, trayIcons);
                }
                catch
                {
                    // Fallback if system tray is unavailable on the platform
                }

                // 5. Run async initialization (recent files, command-line arguments)
                await mainViewModel.InitializeAsync(desktop.Args);

                // 6. Listen for incoming file open requests from secondary instances (e.g. File Explorer context menu or CLI)
                SingleInstanceService.SetArgsHandler(files =>
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
                    {
                        RestoreAndFocusWindow(mainWindow);

                        var validFiles = System.Linq.Enumerable.ToArray(
                            System.Linq.Enumerable.Where(files, f => !string.Equals(f, SingleInstanceService.ActivateToken, StringComparison.Ordinal)));

                        if (validFiles.Length > 0)
                        {
                            var parsedArgs = CommandLineParser.ParseArguments(validFiles);
                            foreach (var arg in parsedArgs)
                            {
                                if (System.IO.File.Exists(arg.FilePath))
                                {
                                    await mainViewModel.OpenFileInternalAsync(arg.FilePath, arg.Line, arg.Column);
                                }
                            }
                        }
                    });
                });
            }
            catch (Exception ex)
            {
                Program.LogCrash(ex);
                desktop.Shutdown(1);
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void RestoreAndFocusWindow(MainWindow mainWindow)
    {
        if (!mainWindow.IsVisible)
        {
            mainWindow.Show();
        }
        if (mainWindow.WindowState == Avalonia.Controls.WindowState.Minimized)
        {
            mainWindow.WindowState = Avalonia.Controls.WindowState.Normal;
        }
        mainWindow.Topmost = true;
        mainWindow.Topmost = false;
        mainWindow.Activate();
        mainWindow.Focus();
    }
}
