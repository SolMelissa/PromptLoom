// FIX: Introduce UI dialog service wrapper to allow view models to be tested without MessageBox.
// CAUSE: Direct MessageBox calls in view models made unit tests depend on WPF UI.
// CHANGE: Add IUiDialogService and a default WPF implementation. 2025-12-25

using System.Windows;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for showing UI dialogs.
/// </summary>
public interface IUiDialogService
{
    /// <summary>
    /// Shows an error dialog.
    /// </summary>
    void ShowError(string message, string title);
}

/// <summary>
/// WPF-backed dialog service.
/// </summary>
public sealed class UiDialogService : IUiDialogService
{
    /// <summary>
    /// Shows an error dialog using MessageBox.
    /// </summary>
    public void ShowError(string message, string title)
        => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
}
