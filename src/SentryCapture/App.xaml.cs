using System;
using System.Windows;
using System.Windows.Threading;
using SentryCapture.Models;
using SentryCapture.Services;
using SentryCapture.ViewModels;

namespace SentryCapture;

public partial class App : Application
{
    public CaptureManager? Manager { get; private set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Never let an unhandled exception silently kill the collector.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                Logger.Instance.Error("Unhandled (non-UI) exception.", ex);
        };

        AppPaths.EnsureDirectories();
        Logger.Instance.Info($"Sentry Capture starting. Data folder: {AppPaths.DataRoot}");

        AppConfig config = ConfigStore.Load();
        Manager = new CaptureManager(config);

        var viewModel = new MainViewModel(Manager);
        var window = new MainWindow { DataContext = viewModel };
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Logger.Instance.Error("Unhandled UI exception (recovered).", e.Exception);
        MessageBox.Show(
            "An unexpected error occurred but Sentry Capture will keep running.\n\n" +
            "Details have been written to the debug log.\n\n" + e.Exception.Message,
            "Sentry Capture", MessageBoxButton.OK, MessageBoxImage.Warning);
        e.Handled = true;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            Logger.Instance.Info("Sentry Capture shutting down.");
            Manager?.Dispose();
        }
        catch { /* ignore shutdown errors */ }
        base.OnExit(e);
    }
}
