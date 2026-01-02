using System;
using System.IO;
using System.Threading;
using System.Windows;
using PromptLoom.Services;

namespace PromptLoom;

public static class Program
{
    /// <summary>
    /// Explicit entry point.
    ///
    /// WPF's generated entry point can terminate the process before App.OnStartup
    /// if App.xaml fails to load or if initialization throws early. This entry point
    /// guarantees we create the session log and surface the exception.
    /// </summary>
    [STAThread]
    public static int Main()
    {
        ErrorReporter? errors = null;
        string? bootstrapPath = null;

        try
        {
            // Bootstrap log: if anything fails before the main logger spins up, we still get a file.
            try
            {
                bootstrapPath = Path.Combine(Path.GetTempPath(), $"promptloom_bootstrap_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                File.AppendAllText(bootstrapPath, "PromptLoom starting..." + Environment.NewLine);
            }
            catch
            {
                bootstrapPath = null;
            }

            errors = ErrorReporter.Instance;
            errors.Info("Program.Main begin");

            var app = new App();
            errors.Info("App constructed");

            // IMPORTANT: InitializeComponent loads App.xaml. If that XAML fails, it can crash
            // before App.OnStartup. We catch it here.
            app.InitializeComponent();
            errors.Info("App.InitializeComponent completed");

            var rc = app.Run();
            errors.Info("App.Run returned: " + rc);
            return rc;
        }
        catch (Exception ex)
        {
            try
            {
                errors ??= ErrorReporter.Instance;
                errors.Report(ex, "Program.Main");
            }
            catch
            {
                // Last resort: write to a temp file.
                try
                {
                    var p = bootstrapPath ?? Path.Combine(Path.GetTempPath(), $"promptloom_fatal_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                    File.WriteAllText(p, ex.ToString());
                }
                catch { }
            }

            try
            {
                MessageBox.Show(
                    "PromptLoom failed to start.\n\n" + ex.Message +
                    "\n\nA log was written to AppData\\Local\\PromptLoom\\Logs or Temp.\n\n" +
                    (bootstrapPath is null ? "" : ("Bootstrap log: " + bootstrapPath)),
                    "PromptLoom startup error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            catch { }

            return -1;
        }
    }
}
