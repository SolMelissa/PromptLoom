namespace SwarmUi.Client;

/// <summary>
/// A pragmatic Text2Image request model.
/// SwarmUI supports many more fields; add them to Extra as needed.
/// </summary>
public sealed class GenerateText2ImageRequest
{
    public string Prompt { get; init; } = "";
    public string NegativePrompt { get; init; } = "";

    /// <summary>
    /// The model name/path to use. SwarmUI's API generally requires this and does not reliably
    /// inherit the Web UI's currently selected model.
    /// </summary>
    public string? Model { get; init; }

    public int Width { get; init; } = 1024;
    public int Height { get; init; } = 1024;

    /// <summary>
    /// Optional steps override. If null, SwarmUI will use its default.
    /// </summary>
    public int? Steps { get; init; } = null;

    /// <summary>
    /// Optional CFG scale override. If null, SwarmUI will use its default.
    /// </summary>
    public double? CfgScale { get; init; } = null;

    public string? Sampler { get; init; }
    public long? Seed { get; init; }

    /// <summary>
    /// Pass-through fields for SwarmUI-specific knobs (loras, refiner, batching, etc.).
    /// </summary>
    public Dictionary<string, object?> Extra { get; init; } = new();
}
