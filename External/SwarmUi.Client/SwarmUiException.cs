namespace SwarmUi.Client;

public sealed class SwarmUiException : Exception
{
    public int StatusCode { get; }

    public SwarmUiException(int statusCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
    }
}
