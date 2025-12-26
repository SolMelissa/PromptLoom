// FIX: Introduce a SwarmUI client factory seam for testability.
// CAUSE: MainViewModel constructed SwarmUiClient directly, preventing mock injection.
// CHANGE: Add ISwarmUiClientFactory and a default implementation. 2025-12-25

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SwarmUi.Client;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction over SwarmUI client methods used by the app.
/// </summary>
public interface IAppSwarmUiClient
{
    /// <summary>Lightweight status check.</summary>
    Task<SwarmStatus> GetCurrentStatusAsync(CancellationToken ct = default);
    /// <summary>Lists available Stable-Diffusion models (best effort).</summary>
    Task<List<string>> ListStableDiffusionModelsAsync(int depth = 6, CancellationToken ct = default);
    /// <summary>Lists available LoRA files (best effort).</summary>
    Task<List<string>> ListLorasAsync(int depth = 6, CancellationToken ct = default);
    /// <summary>Interrupts any waiting/running generations for this session.</summary>
    Task<bool> InterruptAllAsync(bool otherSessions = false, CancellationToken ct = default);
    /// <summary>Starts a generation via WebSocket, yielding progress frames.</summary>
    IAsyncEnumerable<string> GenerateText2ImageStreamAsync(GenerateText2ImageRequest request, CancellationToken ct = default);
    /// <summary>Suggests a model and resolution if the server requires them.</summary>
    Task<(string? model, int width, int height)> GetSuggestedModelAndResolutionAsync(CancellationToken ct = default);
}

/// <summary>
/// Factory for creating SwarmUI clients.
/// </summary>
public interface ISwarmUiClientFactory
{
    /// <summary>
    /// Creates a new SwarmUI client for the given base URI and token.
    /// </summary>
    IAppSwarmUiClient Create(Uri baseUri, string? swarmToken);
}

/// <summary>
/// Default SwarmUI client factory.
/// </summary>
public sealed class SwarmUiClientFactory : ISwarmUiClientFactory
{
    /// <inheritdoc/>
    public IAppSwarmUiClient Create(Uri baseUri, string? swarmToken)
    {
        var options = new SwarmUiClientOptions
        {
            BaseUrl = baseUri,
            AutoSession = true,
            SwarmToken = string.IsNullOrWhiteSpace(swarmToken) ? null : swarmToken,
            // Timeout is controlled via CancellationToken + user cancellation UI.
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        };

        return new SwarmUiClientAdapter(new SwarmUiClient(options));
    }
}

/// <summary>
/// Adapter that exposes the SwarmUiClient surface used by the app.
/// </summary>
public sealed class SwarmUiClientAdapter : IAppSwarmUiClient
{
    private readonly SwarmUiClient _inner;

    /// <summary>
    /// Creates a new adapter.
    /// </summary>
    public SwarmUiClientAdapter(SwarmUiClient inner)
    {
        _inner = inner;
    }

    /// <inheritdoc/>
    public Task<SwarmStatus> GetCurrentStatusAsync(CancellationToken ct = default)
        => _inner.GetCurrentStatusAsync(ct);

    /// <inheritdoc/>
    public Task<List<string>> ListStableDiffusionModelsAsync(int depth = 6, CancellationToken ct = default)
        => _inner.ListStableDiffusionModelsAsync(depth, ct);

    /// <inheritdoc/>
    public Task<List<string>> ListLorasAsync(int depth = 6, CancellationToken ct = default)
        => _inner.ListLorasAsync(depth, ct);

    /// <inheritdoc/>
    public Task<bool> InterruptAllAsync(bool otherSessions = false, CancellationToken ct = default)
        => _inner.InterruptAllAsync(otherSessions, ct);

    /// <inheritdoc/>
    public IAsyncEnumerable<string> GenerateText2ImageStreamAsync(GenerateText2ImageRequest request, CancellationToken ct = default)
        => _inner.GenerateText2ImageStreamAsync(request, ct);

    /// <inheritdoc/>
    public Task<(string? model, int width, int height)> GetSuggestedModelAndResolutionAsync(CancellationToken ct = default)
        => _inner.GetSuggestedModelAndResolutionAsync(ct);
}
