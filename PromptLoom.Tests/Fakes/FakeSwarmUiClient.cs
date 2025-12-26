// TEST: Fake SwarmUI client/factory for unit tests.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PromptLoom.Services;
using SwarmUi.Client;

namespace PromptLoom.Tests.Fakes;

public sealed class FakeSwarmUiClient : IAppSwarmUiClient
{
    public int StatusCalls { get; private set; }
    public int ModelCalls { get; private set; }
    public int LoraCalls { get; private set; }

    public SwarmStatus Status { get; set; } = new SwarmStatus { Status = "OK", Detail = "test" };
    public List<string> Models { get; } = new();
    public List<string> Loras { get; } = new();
    public (string? model, int width, int height) Suggested { get; set; } = ("model", 512, 512);

    public Task<SwarmStatus> GetCurrentStatusAsync(CancellationToken ct = default)
    {
        StatusCalls++;
        return Task.FromResult(Status);
    }

    public Task<List<string>> ListStableDiffusionModelsAsync(int depth = 6, CancellationToken ct = default)
    {
        ModelCalls++;
        return Task.FromResult(Models);
    }

    public Task<List<string>> ListLorasAsync(int depth = 6, CancellationToken ct = default)
    {
        LoraCalls++;
        return Task.FromResult(Loras);
    }
    public Task<bool> InterruptAllAsync(bool otherSessions = false, CancellationToken ct = default) => Task.FromResult(true);
    public async IAsyncEnumerable<string> GenerateText2ImageStreamAsync(
        GenerateText2ImageRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }
    public Task<(string? model, int width, int height)> GetSuggestedModelAndResolutionAsync(CancellationToken ct = default)
        => Task.FromResult(Suggested);
}

public sealed class FakeSwarmUiClientFactory : ISwarmUiClientFactory
{
    private readonly IAppSwarmUiClient _client;

    public FakeSwarmUiClientFactory(IAppSwarmUiClient client)
    {
        _client = client;
    }

    public IAppSwarmUiClient Create(Uri baseUri, string? swarmToken) => _client;
}
