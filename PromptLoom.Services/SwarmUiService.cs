// FIX: Extract SwarmUI operations behind a service seam for view-model simplicity.
// CAUSE: MainViewModel directly constructed Swarm clients and handled URL parsing.
// CHANGE: Introduce IAppSwarmUiService with a default implementation. 2025-12-27

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SwarmUi.Client;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for SwarmUI operations used by the app.
/// </summary>
public interface IAppSwarmUiService
{
    /// <summary>
    /// Validates and returns the base URI for SwarmUI.
    /// </summary>
    Uri GetBaseUri(string swarmUrl);

    /// <summary>
    /// Checks current status for the SwarmUI server.
    /// </summary>
    Task<SwarmStatus> GetStatusAsync(string swarmUrl, string? swarmToken, CancellationToken ct = default);

    /// <summary>
    /// Lists available Stable-Diffusion models.
    /// </summary>
    Task<List<string>> ListModelsAsync(string swarmUrl, string? swarmToken, int depth = 6, CancellationToken ct = default);

    /// <summary>
    /// Lists available LoRAs.
    /// </summary>
    Task<List<string>> ListLorasAsync(string swarmUrl, string? swarmToken, int depth = 6, CancellationToken ct = default);

    /// <summary>
    /// Suggests a model and resolution if required by the server.
    /// </summary>
    Task<(string? model, int width, int height)> GetSuggestedModelAndResolutionAsync(string swarmUrl, string? swarmToken, CancellationToken ct = default);

    /// <summary>
    /// Starts a generation via WebSocket, yielding progress frames.
    /// </summary>
    IAsyncEnumerable<string> GenerateText2ImageStreamAsync(string swarmUrl, string? swarmToken, GenerateText2ImageRequest request, CancellationToken ct = default);

    /// <summary>
    /// Interrupts any running generations for the current session.
    /// </summary>
    Task<bool> InterruptAllAsync(string swarmUrl, string? swarmToken, bool otherSessions = false, CancellationToken ct = default);
}

/// <summary>
/// Default SwarmUI service using the SwarmUI client factory.
/// </summary>
public sealed class SwarmUiService : IAppSwarmUiService
{
    private readonly ISwarmUiClientFactory _factory;

    public SwarmUiService(ISwarmUiClientFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public Uri GetBaseUri(string swarmUrl)
    {
        if (!Uri.TryCreate(swarmUrl, UriKind.Absolute, out var baseUri))
            throw new InvalidOperationException($"Invalid SwarmUrl: '{swarmUrl}'. Expected something like http://127.0.0.1:7801");
        return baseUri;
    }

    public Task<SwarmStatus> GetStatusAsync(string swarmUrl, string? swarmToken, CancellationToken ct = default)
        => CreateClient(swarmUrl, swarmToken).GetCurrentStatusAsync(ct);

    public Task<List<string>> ListModelsAsync(string swarmUrl, string? swarmToken, int depth = 6, CancellationToken ct = default)
        => CreateClient(swarmUrl, swarmToken).ListStableDiffusionModelsAsync(depth, ct);

    public Task<List<string>> ListLorasAsync(string swarmUrl, string? swarmToken, int depth = 6, CancellationToken ct = default)
        => CreateClient(swarmUrl, swarmToken).ListLorasAsync(depth, ct);

    public Task<(string? model, int width, int height)> GetSuggestedModelAndResolutionAsync(string swarmUrl, string? swarmToken, CancellationToken ct = default)
        => CreateClient(swarmUrl, swarmToken).GetSuggestedModelAndResolutionAsync(ct);

    public IAsyncEnumerable<string> GenerateText2ImageStreamAsync(string swarmUrl, string? swarmToken, GenerateText2ImageRequest request, CancellationToken ct = default)
        => CreateClient(swarmUrl, swarmToken).GenerateText2ImageStreamAsync(request, ct);

    public Task<bool> InterruptAllAsync(string swarmUrl, string? swarmToken, bool otherSessions = false, CancellationToken ct = default)
        => CreateClient(swarmUrl, swarmToken).InterruptAllAsync(otherSessions, ct);

    private IAppSwarmUiClient CreateClient(string swarmUrl, string? swarmToken)
        => _factory.Create(GetBaseUri(swarmUrl), swarmToken);
}
