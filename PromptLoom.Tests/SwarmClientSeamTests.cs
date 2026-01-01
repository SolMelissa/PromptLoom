// CHANGE LOG
// - 2025-12-31 | Request: SwarmUI cards | Assert against model/LoRA card names.
// TEST: Validate SwarmUI seam integration via injected factory and error reporter.

using PromptLoom.Tests.Fakes;
using PromptLoom.ViewModels;

namespace PromptLoom.Tests;

/// <summary>
/// Tests for SwarmUI client seam behaviors.
/// </summary>
public class SwarmClientSeamTests
{
    /// <summary>
    /// Loads models into the view model using the injected Swarm client factory.
    /// </summary>
    [Fact]
    public void RefreshSwarmModels_UsesInjectedClient()
    {
        var fakeClient = new FakeSwarmUiClient();
        fakeClient.Models.AddRange(new[] { "ModelA", "ModelB" });

        var vm = new MainViewModel(
            dispatcher: new ImmediateDispatcherService(),
            swarmClientFactory: new FakeSwarmUiClientFactory(fakeClient),
            errorReporter: new FakeErrorReporter());

        vm.RefreshSwarmModels();

        Assert.Contains(vm.SwarmModels, m => m.Name == "ModelA");
        Assert.Contains(vm.SwarmModels, m => m.Name == "ModelB");
    }

    /// <summary>
    /// Loads LoRAs into the view model using the injected Swarm client factory.
    /// </summary>
    [Fact]
    public void RefreshSwarmLoras_UsesInjectedClient()
    {
        var fakeClient = new FakeSwarmUiClient();
        fakeClient.Loras.AddRange(new[] { "LoraA", "LoraB" });

        var vm = new MainViewModel(
            dispatcher: new ImmediateDispatcherService(),
            swarmClientFactory: new FakeSwarmUiClientFactory(fakeClient),
            errorReporter: new FakeErrorReporter());

        vm.RefreshSwarmLoras();

        Assert.Contains(vm.SwarmLoras, l => l.Name == "LoraA");
        Assert.Contains(vm.SwarmLoras, l => l.Name == "LoraB");
    }
}
