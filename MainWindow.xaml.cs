using System;
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
    public MainWindow()
    {
        ErrorReporter.Instance.Info("MainWindow ctor begin");
        InitializeComponent();
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
                    ErrorReporter.Instance.Info("MainWindow creating MainViewModel");
                    var vm = new MainViewModel();
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
