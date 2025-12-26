// TEST: Fake clipboard service for view model smoke tests.

using PromptLoom.Services;

namespace PromptLoom.Tests.Fakes;

public sealed class FakeClipboardService : IClipboardService
{
    public string? LastText { get; private set; }

    public void SetText(string text)
    {
        LastText = text;
    }
}
