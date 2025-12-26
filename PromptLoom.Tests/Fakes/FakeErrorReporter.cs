// TEST: Fake error reporter for verifying logging without static state.

using System;
using System.Collections.ObjectModel;
using PromptLoom.Services;

namespace PromptLoom.Tests.Fakes;

public sealed class FakeErrorReporter : IErrorReporter
{
    public ObservableCollection<string> Entries { get; } = new();
    public string LatestMessage { get; private set; } = "";

    public int UiEventCount { get; private set; }
    public int ReportCount { get; private set; }

    public void Info(string message, string? context = null) => LatestMessage = message;
    public void Debug(string message, string? context = null) => LatestMessage = message;
    public void Error(string message, string? context = null) => LatestMessage = message;
    public void UiEvent(string eventName, object? data = null) => UiEventCount++;
    public void Report(Exception ex, string? context = null) => ReportCount++;
}
