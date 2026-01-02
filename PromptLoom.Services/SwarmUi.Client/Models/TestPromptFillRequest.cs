namespace SwarmUi.Client;

/// <summary>
/// Mirrors SwarmUI's documented TestPromptFill request shape:
/// - prompt (required)
/// Additional fields may be supported by specific Swarm builds; use <see cref="Extra"/> to pass them through.
/// </summary>
public sealed class TestPromptFillRequest
{
    public string Prompt { get; init; } = "";

    /// <summary>
    /// Optional pass-through fields for SwarmUI-specific knobs.
    /// </summary>
    public Dictionary<string, object?> Extra { get; init; } = new();
}
