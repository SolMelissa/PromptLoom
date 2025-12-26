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

        Assert.Contains("ModelA", vm.SwarmModels);
        Assert.Contains("ModelB", vm.SwarmModels);
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

        Assert.Contains("LoraA", vm.SwarmLoras);
        Assert.Contains("LoraB", vm.SwarmLoras);
    }
}
