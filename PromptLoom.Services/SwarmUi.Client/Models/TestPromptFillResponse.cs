namespace SwarmUi.Client;

public sealed class TestPromptFillResponse
{
    /// <summary>
    /// The filled prompt, returned by SwarmUI as "result" per docs.
    /// </summary>
    public string FilledPrompt { get; init; } = "";

    /// <summary>Any extra fields returned by SwarmUI.</summary>
    public Dictionary<string, object?> Extra { get; init; } = new();
}
