// TEST: Add persistence-related view model tests using file system and AppData fakes.

using System;
using System.IO;
using PromptLoom.Services;
using PromptLoom.Tests.Fakes;
using PromptLoom.ViewModels;

namespace PromptLoom.Tests;

/// <summary>
/// Tests for view model behaviors that touch persistence seams.
/// </summary>
public class MainViewModelPersistenceTests
{
    private sealed class FakeUserSettingsStore : IUserSettingsStore
    {
        public UserSettings? Settings { get; set; }
        public UserSettings? Load() => Settings;
        public void Save(UserSettings settings) => Settings = settings;
    }

    /// <summary>
    /// Loads user settings from the injected file system on construction.
    /// </summary>
    [Fact]
    public void Constructor_LoadsUserSettingsFromFileSystem()
    {
        var root = "C:\\FakeRoot";
        var appData = new FakeAppDataStore(root);
        var fs = new FakeFileSystem();
        fs.AddFile(Path.Combine(AppContext.BaseDirectory, "PromptLoom.csproj"), "");
        var settings = new UserSettings
        {
            SwarmUrl = "http://localhost:1234",
            SwarmToken = "token",
            SendSwarmSteps = true,
            SwarmSteps = 12
        };
        var store = new FakeUserSettingsStore { Settings = settings };

        var vm = new MainViewModel(
            fileSystem: fs,
            appDataStore: appData,
            dispatcher: new ImmediateDispatcherService(),
            settingsStore: store);

        Assert.Equal("http://localhost:1234", vm.SwarmUrl);
        Assert.Equal("token", vm.SwarmToken);
        Assert.True(vm.SendSwarmSteps);
        Assert.Equal(12, vm.SwarmSteps);
    }

    /// <summary>
    /// Saves the prompt output using the injected file system.
    /// </summary>
    [Fact]
    public void SaveOutputCommand_WritesPromptToOutputDir()
    {
        var root = "C:\\FakeRoot";
        var appData = new FakeAppDataStore(root);
        var fs = new FakeFileSystem();
        fs.AddFile(Path.Combine(AppContext.BaseDirectory, "PromptLoom.csproj"), "");
        var store = new FakeUserSettingsStore();

        var vm = new MainViewModel(
            fileSystem: fs,
            appDataStore: appData,
            dispatcher: new ImmediateDispatcherService(),
            settingsStore: store);

        vm.PromptText = "hello world";
        vm.SaveOutputCommand.Execute(null);

        var expectedPrefix = Path.Combine(root, "Output");
        var wroteFile = fs.GetFiles(expectedPrefix, "*.txt", SearchOption.TopDirectoryOnly);

        Assert.NotEmpty(wroteFile);
        var first = wroteFile.GetEnumerator();
        first.MoveNext();
        var path = first.Current;
        Assert.StartsWith(expectedPrefix, path, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("hello world", fs.ReadAllText(path));
    }
}
