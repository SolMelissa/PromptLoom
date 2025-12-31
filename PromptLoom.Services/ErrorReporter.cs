// Build notes:
// - 2025-12-22: Error CS1061: 'ErrorReporter' missing method 'Error' (called from AppDataStore).
//   Fix: added Error(string message, string? context) to log ERR lines without needing an Exception.
// - 2025-12-22: Error CS0111: duplicate member Error(string, string?) defined twice.
//   Fix: removed the duplicate method body and kept a single canonical Error(...) method.

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;

namespace PromptLoom.Services;

/// <summary>
/// Centralized error reporting.
/// 
/// Goals:
/// - Never crash silently.
/// - Keep a lightweight in-app log for the current session.
/// - Persist a timestamped log file to AppData for post-mortems.
/// </summary>
public sealed class ErrorReporter
{
    // Lazy init so we can survive failures in the constructor and fall back cleanly.
    private static readonly Lazy<ErrorReporter> _lazy = new(() => new ErrorReporter());
    public static ErrorReporter Instance => _lazy.Value;

    public ObservableCollection<string> Entries { get; } = new();

    public string LatestMessage { get; private set; } = "";

    private readonly string _logDir;
    private readonly string _logPath;
    private readonly object _lock = new();

    private ErrorReporter()
    {
        // Be extremely defensive here: if anything throws, the app can crash before a log exists.
        // We prefer AppData\Local, but we can fall back to Temp.
        try
        {
            _logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PromptLoom", "Logs");
            Directory.CreateDirectory(_logDir);
            _logPath = Path.Combine(_logDir, $"promptloom_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }
        catch
        {
            _logDir = Path.GetTempPath();
            _logPath = Path.Combine(_logDir, $"promptloom_fallback_{DateTime.Now:yyyyMMdd_HHmmss}.log");
        }

        try
        {
            File.AppendAllText(_logPath, "=== PromptLoom session started ===" + Environment.NewLine);
        }
        catch
        {
            // If even this fails, we still keep in-memory Entries.
        }

        // Avoid touching WPF Dispatcher / UI-bound collections during construction.
        // We'll start populating Entries lazily when the app is running.
    }

    public void Info(string message, string? context = null)
        => WriteLine(FormatLine("INFO", message, context));

    public void Debug(string message, string? context = null)
        => WriteLine(FormatLine("DBG", message, context));

    /// <summary>
    /// Log an error message (without requiring an Exception instance).
    /// </summary>
    public void Error(string message, string? context = null)
        => WriteLine(FormatLine("ERR", message, context));

    /// <summary>
    /// Log a user interaction or noteworthy UI event.
    /// If <paramref name="data"/> is provided, it will be written as compact JSON.
    /// </summary>
    public void UiEvent(string eventName, object? data = null)
    {
        try
        {
            if (data is null)
            {
                WriteLine(FormatLine("UI", eventName, null));
                return;
            }

            var payload = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = false,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            WriteLine(FormatLine("UI", $"{eventName} {payload}", null));
        }
        catch
        {
            // Never let logging break the app.
            WriteLine(FormatLine("UI", eventName, null));
        }
    }

    public void Report(Exception ex, string? context = null)
        => WriteLine(FormatLine("ERR", ex.ToString(), context));

    private string FormatLine(string kind, string message, string? context)
    {
        var ctx = string.IsNullOrWhiteSpace(context) ? "" : $" [{context}]";
        return $"{DateTime.Now:HH:mm:ss} {kind}{ctx} {message}";
    }

    private void WriteLine(string line)
    {
        LatestMessage = line;

        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logPath, line + Environment.NewLine);
            }
            catch
            {
                // If disk logging fails, we still keep the in-memory entries.
            }
        }

        // Keep in-app list reasonably sized.
        // IMPORTANT: ObservableCollection must be mutated on the UI thread in WPF.
        void AddLine()
        {
            try
            {
                Entries.Add(line);
                while (Entries.Count > 500)
                    Entries.RemoveAt(0);
            }
            catch
            {
                // Ignore UI list failures.
            }
        }

        try
        {
            var disp = Application.Current?.Dispatcher;
            if (disp is not null)
            {
                if (!disp.CheckAccess())
                    disp.BeginInvoke(AddLine);
                else
                    AddLine();
            }
            // If Application.Current is null (very early startup), skip Entries entirely.
        }
        catch
        {
            // swallow
        }
    }
}
