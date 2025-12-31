// CHANGE LOG
// - 2025-12-29 | Request: Versioned titlebar | Display MAJOR.MINOR.PATCH #COMMITCOUNT in the window title.
// - 2025-12-25 | Request: Allow injected MainViewModel | Enabled constructor injection for DI/testing.
//
// FIX: Allow MainWindow to accept an injected MainViewModel instead of constructing it directly.
// CAUSE: View-owned VM creation was an MVVM violation and blocked simple DI/testing.
// CHANGE: Add constructor injection and reuse existing deferred Initialize. 2025-12-25

using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Threading;
using System.Windows.Input;
using PromptLoom.Services;
using PromptLoom.ViewModels;

namespace PromptLoom;

public partial class MainWindow : Window
{
    private readonly MainViewModel? _viewModel;

    /// <summary>
    /// Creates a new main window with an optional injected view model.
    /// </summary>
    public MainWindow(MainViewModel? viewModel)
    {
        _viewModel = viewModel;
        ErrorReporter.Instance.Info("MainWindow ctor begin");
        InitializeComponent();
        Title = BuildWindowTitle();
        ErrorReporter.Instance.Info("MainWindow InitializeComponent done");

        // Lightweight global UI telemetry hooks.
        // We use routed event handlers so we don't need to wire each TabControl individually.
        AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler(OnAnySelectionChanged), true);
        AddHandler(Button.ClickEvent, new RoutedEventHandler(OnAnyButtonClicked), true);

        // Defer VM creation until the window is visible, so any errors are user-facing.
        Loaded += (_, __) =>
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    var vm = _viewModel ?? new MainViewModel();
                    ErrorReporter.Instance.Info("MainWindow using MainViewModel");
                    DataContext = vm;
                    ErrorReporter.Instance.Info("MainWindow DataContext set");
                    vm.Initialize();
                    ErrorReporter.Instance.Info("MainWindow ViewModel initialized");
                }
                catch (Exception ex)
                {
                    ErrorReporter.Instance.Report(ex, "MainWindow.InitViewModel");
                    MessageBox.Show(
                        "PromptLoom failed to initialize.\n\n" + ex.Message +
                        "\n\nDetails were written to the session log (AppData/Local/PromptLoom/Logs).",
                        "PromptLoom initialization error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }), DispatcherPriority.Loaded);
        };
    }

    /// <summary>
    /// Creates a new main window (design-time or fallback path).
    /// </summary>
    public MainWindow()
        : this(null)
    {
    }

    private static string BuildWindowTitle()
    {
        var fileVersion = FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
        if (string.IsNullOrWhiteSpace(fileVersion))
            return "PromptLoom";

        var parts = fileVersion.Split('.');
        if (parts.Length < 4)
            return $"PromptLoom {fileVersion}";

        return $"PromptLoom {parts[0]}.{parts[1]}.{parts[2]} #{parts[3]}";
    }

    private void OnAnyButtonClicked(object sender, RoutedEventArgs e)
    {
        try
        {
            if (e.OriginalSource is Button b)
            {
                var label = b.Content?.ToString();
                if (!string.IsNullOrWhiteSpace(label))
                    ErrorReporter.Instance.UiEvent("Button.Click", new { content = label });
            }
        }
        catch { }
    }


    private void OnAnySelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        try
        {
            // Only record TabControl selection changes (we avoid logging every ListBox/ComboBox change).
            if (e.OriginalSource is TabControl tab)
            {
                var header = (tab.SelectedItem as TabItem)?.Header?.ToString() ?? tab.SelectedItem?.ToString();
                ErrorReporter.Instance.UiEvent("Tab.Select", new { tab = tab.Name, selected = header });
            }
        }
        catch { }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

	// Close the overlay when clicking the dark background (but not when clicking the image or info bar).
	private void ImageOverlayRoot_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if (sender is not Grid root)
			return;

		// Only close when the click hits the root background itself.
		if (e.OriginalSource == root && DataContext is MainViewModel vm && vm.HideImageOverlayCommand.CanExecute(null))
		{
			vm.HideImageOverlayCommand.Execute(null);
			e.Handled = true;
		}
	}


	private void MainWindow_OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
    if (DataContext is not MainViewModel vm || !vm.IsImageOverlayVisible)
        return;

    // Navigation among completed images only
    if (e.Key == System.Windows.Input.Key.Left)
    {
        if (vm.OverlayPrevCommand.CanExecute(null)) vm.OverlayPrevCommand.Execute(null);
        e.Handled = true;
        return;
    }
    if (e.Key == System.Windows.Input.Key.Right)
    {
        if (vm.OverlayNextCommand.CanExecute(null)) vm.OverlayNextCommand.Execute(null);
        e.Handled = true;
        return;
    }

    // Zoom with Up/Down arrows
    if (e.Key == System.Windows.Input.Key.Up)
    {
        OverlayZoomPan?.ZoomIn();
        e.Handled = true;
        return;
    }
    if (e.Key == System.Windows.Input.Key.Down)
    {
        OverlayZoomPan?.ZoomOut();
        e.Handled = true;
        return;
    }
	}

}
