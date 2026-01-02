namespace SwarmUi.Client;

/// <summary>
/// Represents an API-level error returned by SwarmUI in JSON
/// (often with HTTP 200 but fields like "error" and/or "error_id").
/// </summary>
public sealed class SwarmUiApiErrorException : Exception
{
    public string? ErrorId { get; }

    public SwarmUiApiErrorException(string? errorId, string message)
        : base(message)
    {
        ErrorId = errorId;
    }
}
