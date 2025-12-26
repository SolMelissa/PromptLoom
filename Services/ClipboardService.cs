// FIX: Introduce clipboard service wrapper to allow view models to be tested without static Clipboard.
// CAUSE: Direct Clipboard.SetText calls in view models were hard to mock in tests.
// CHANGE: Add IClipboardService and a default WPF implementation. 2025-12-25

using System.Windows;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for clipboard operations.
/// </summary>
public interface IClipboardService
{
    /// <summary>
    /// Sets the clipboard text.
    /// </summary>
    void SetText(string text);
}

/// <summary>
/// WPF-backed clipboard service.
/// </summary>
public sealed class ClipboardService : IClipboardService
{
    /// <summary>
    /// Sets the clipboard text via WPF Clipboard.
    /// </summary>
    public void SetText(string text) => Clipboard.SetText(text);
}
