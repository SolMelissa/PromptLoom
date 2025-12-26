// TEST: Fake UI dialog service for view model smoke tests.

using PromptLoom.Services;

namespace PromptLoom.Tests.Fakes;

public sealed class FakeUiDialogService : IUiDialogService
{
    public string? LastMessage { get; private set; }
    public string? LastTitle { get; private set; }

    public void ShowError(string message, string title)
    {
        LastMessage = message;
        LastTitle = title;
    }
}
