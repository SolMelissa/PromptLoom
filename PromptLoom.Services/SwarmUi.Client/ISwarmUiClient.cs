namespace SwarmUi.Client;

/// <summary>
/// Core SwarmUI API client surface intended for prompt generation apps.
/// </summary>
public interface ISwarmUiClient
{
    /// <summary>Current session id, if acquired.</summary>
    string? SessionId { get; }

    /// <summary>Ensures a valid session id exists (creates one if missing).</summary>
    Task<string> EnsureSessionAsync(CancellationToken ct = default);

    /// <summary>Calls a SwarmUI route: POST /API/{routeName} with a JSON body.</summary>
    Task<TResponse> CallRouteAsync<TRequest, TResponse>(string routeName, TRequest request, CancellationToken ct = default);

    /// <summary>Lightweight status check.</summary>
    Task<SwarmStatus> GetCurrentStatusAsync(CancellationToken ct = default);

    /// <summary>Expands Swarm prompt wildcards/randoms server side.</summary>
    Task<TestPromptFillResponse> TestPromptFillAsync(TestPromptFillRequest request, CancellationToken ct = default);

    /// <summary>Starts a Text2Image generation via HTTP request/response.</summary>
    Task<GenerateText2ImageResponse> GenerateText2ImageAsync(GenerateText2ImageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Interrupts any waiting/running generations for this session.
    /// </summary>
    Task<bool> InterruptAllAsync(bool otherSessions = false, CancellationToken ct = default);

    /// <summary>Lists available Stable-Diffusion models (best effort).</summary>
    Task<List<string>> ListStableDiffusionModelsAsync(int depth = 6, CancellationToken ct = default);

    /// <summary>Lists available LoRA files (best effort).</summary>
    Task<List<string>> ListLorasAsync(int depth = 6, CancellationToken ct = default);

    /// <summary>
    /// Starts a generation via WebSocket, yielding progress messages as raw JSON strings.
    /// This stays flexible because SwarmUI’s WS message shapes can vary by version/config.
    /// </summary>
    IAsyncEnumerable<string> GenerateText2ImageStreamAsync(GenerateText2ImageRequest request, CancellationToken ct = default);
}
