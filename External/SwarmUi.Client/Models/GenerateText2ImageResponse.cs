namespace SwarmUi.Client;

public sealed class GenerateText2ImageResponse
{
    /// <summary>
    /// SwarmUI often returns image references/paths and metadata.
    /// Keep this flexible by preserving Extra, while exposing a few common fields.
    /// </summary>
    public string? Image { get; init; }
    public List<string> Images { get; init; } = new();

    public long? Seed { get; init; }
    public string? Prompt { get; init; }
    public string? NegativePrompt { get; init; }

    public Dictionary<string, object?> Extra { get; init; } = new();
}
