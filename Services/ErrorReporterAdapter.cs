// FIX: Add an error reporter abstraction to support testing without static state.
// CAUSE: Logic paths used ErrorReporter.Instance directly, which is hard to replace in tests.
// CHANGE: Introduce IErrorReporter and a default adapter. 2025-12-25

using System;
using System.Collections.ObjectModel;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for error reporting and UI event logging.
/// </summary>
public interface IErrorReporter
{
    /// <summary>
    /// Gets in-memory log entries.
    /// </summary>
    ObservableCollection<string> Entries { get; }

    /// <summary>
    /// Gets the last message written.
    /// </summary>
    string LatestMessage { get; }

    /// <summary>
    /// Writes an informational message.
    /// </summary>
    void Info(string message, string? context = null);

    /// <summary>
    /// Writes a debug message.
    /// </summary>
    void Debug(string message, string? context = null);

    /// <summary>
    /// Writes an error message (without exception).
    /// </summary>
    void Error(string message, string? context = null);

    /// <summary>
    /// Writes a UI event payload.
    /// </summary>
    void UiEvent(string eventName, object? data = null);

    /// <summary>
    /// Writes a full exception report.
    /// </summary>
    void Report(Exception ex, string? context = null);
}

/// <summary>
/// Adapter that delegates to the static ErrorReporter instance.
/// </summary>
public sealed class ErrorReporterAdapter : IErrorReporter
{
    /// <summary>
    /// The underlying ErrorReporter instance.
    /// </summary>
    public ErrorReporter Inner { get; }

    /// <summary>
    /// Creates a new adapter instance.
    /// </summary>
    public ErrorReporterAdapter(ErrorReporter? inner = null)
    {
        Inner = inner ?? ErrorReporter.Instance;
    }

    /// <inheritdoc/>
    public ObservableCollection<string> Entries => Inner.Entries;
    /// <inheritdoc/>
    public string LatestMessage => Inner.LatestMessage;
    /// <inheritdoc/>
    public void Info(string message, string? context = null) => Inner.Info(message, context);
    /// <inheritdoc/>
    public void Debug(string message, string? context = null) => Inner.Debug(message, context);
    /// <inheritdoc/>
    public void Error(string message, string? context = null) => Inner.Error(message, context);
    /// <inheritdoc/>
    public void UiEvent(string eventName, object? data = null) => Inner.UiEvent(eventName, data);
    /// <inheritdoc/>
    public void Report(Exception ex, string? context = null) => Inner.Report(ex, context);
}
