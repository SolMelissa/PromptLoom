using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using PromptLoom.Services;

namespace PromptLoom;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // IMPORTANT:
        // Wire exception handlers BEFORE WPF creates MainWindow (StartupUri / default Startup behavior).
        // Otherwise a constructor/Loaded exception will terminate the process silently (WinExe).

        var errors = ErrorReporter.Instance;
        errors.Info("OnStartup begin");

        // UI thread exceptions.
        DispatcherUnhandledException += (_, args) =>
        {
            errors.Report(args.Exception, "DispatcherUnhandledException");
            MessageBox.Show(
                "PromptLoom hit an unexpected error.\n\n" + args.Exception.Message +
                "\n\nDetails were written to the session log (AppData/Local/PromptLoom/Logs).",
                "PromptLoom error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            // Prevent hard crash where possible.
            args.Handled = true;
        };

        // AppDomain non-UI exceptions.
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
                errors.Report(ex, "AppDomain.UnhandledException");
            else
                errors.Info("Unhandled exception (non-Exception object): " + args.ExceptionObject, "AppDomain.UnhandledException");
        };

        // Background task exceptions.
        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            errors.Report(args.Exception, "UnobservedTaskException");
            args.SetObserved();
        };

        try
        {
            errors.Info("Calling base.OnStartup");
            base.OnStartup(e);
            errors.Info("base.OnStartup returned");

            // We removed StartupUri so we can guarantee errors are visible/logged.
            var window = new MainWindow();
            MainWindow = window;
            errors.Info("Showing MainWindow");
            window.Show();
            errors.Info("MainWindow.Show returned");

            window.Closed += (_, __) =>
            {
                errors.Info("MainWindow closed");
            };
        }
        catch (Exception ex)
        {
            errors.Report(ex, "Startup");
            MessageBox.Show(
                "PromptLoom failed to start.\n\n" + ex.Message +
                "\n\nDetails were written to the session log (AppData/Local/PromptLoom/Logs).",
                "PromptLoom startup error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }
}
