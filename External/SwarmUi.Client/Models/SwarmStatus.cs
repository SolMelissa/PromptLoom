namespace SwarmUi.Client;

public sealed class SwarmStatus
{
    public string? Status { get; init; }
    public string? Detail { get; init; }

    /// <summary>Any extra fields returned by SwarmUI.</summary>
    public Dictionary<string, object?> Extra { get; init; } = new();
}
