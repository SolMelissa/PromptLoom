// CHANGE LOG
// - 2026-01-12 | Fix: Tag search tests | Implement new tag color and top-tag APIs in fake service.
// - 2026-03-09 | Fix: Categories path | Update test file paths to AppData Categories layout.
// - 2026-03-09 | Request: Indexing progress | Update fake tag indexer to match progress-aware interface.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PromptLoom.Services;
using PromptLoom.Tests.Fakes;
using PromptLoom.ViewModels;
using Xunit;

namespace PromptLoom.Tests;

/// <summary>
/// Unit tests for tag prompt generation logic.
/// </summary>
public class TagPromptBuilderTests
{
    /// <summary>
    /// Generates a prompt from selected tag files and validates normalization.
    /// </summary>
    [Fact]
    public void Generate_PromptFromSelectedFiles_NormalizesEntries()
    {
        var fileReader = new FakeWildcardFileReader(new Dictionary<string, List<string>>
        {
            ["a.txt"] = new() { "  hello\r\nworld  " },
            ["b.txt"] = new() { "sparkle" }
        });
        var builder = new TagPromptBuilder(fileReader, new FixedRandomSource());

        var result = builder.Generate(new[] { "a.txt", "b.txt" }, seed: 123);

        Assert.Equal("hello world\nsparkle", result.Prompt);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void Generate_EmptyFiles_ReportMessages()
    {
        var fileReader = new FakeWildcardFileReader(new Dictionary<string, List<string>>
        {
            ["empty.txt"] = new()
        });
        var builder = new TagPromptBuilder(fileReader, new FixedRandomSource());

        var result = builder.Generate(new[] { "empty.txt" }, seed: null);

        Assert.Equal(string.Empty, result.Prompt);
        Assert.Contains(result.Messages, message => message.Contains("has no entries", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.Messages, message => message.Contains("Prompt is empty", StringComparison.OrdinalIgnoreCase));
    }
}

public class MainViewModelSmokeTests
{
    [Fact]
    public void CopyCommand_SetsClipboardText()
    {
        var clipboard = new FakeClipboardService();
        var dispatcher = new ImmediateDispatcherService();
        var fileSystem = new FakeFileSystem();
        var appDataStore = new FakeAppDataStore("C:\\Temp\\PromptLoomTests");
        var vm = new MainViewModel(
            fileSystem: fileSystem,
            appDataStore: appDataStore,
            searchViewModel: BuildSearchViewModel(),
            clipboard: clipboard,
            dispatcher: dispatcher);

        vm.PromptText = "hello";
        vm.CopyCommand.Execute(null);

        Assert.Equal("hello", clipboard.LastText);
        Assert.Equal("Copied!", vm.CopyButtonText);
    }

    [Fact]
    public void OpenSwarmUiCommand_StartsProcess()
    {
        var process = new FakeProcessService();
        var dispatcher = new ImmediateDispatcherService();
        var fileSystem = new FakeFileSystem();
        var appDataStore = new FakeAppDataStore("C:\\Temp\\PromptLoomTests");
        var vm = new MainViewModel(
            fileSystem: fileSystem,
            appDataStore: appDataStore,
            searchViewModel: BuildSearchViewModel(),
            process: process,
            dispatcher: dispatcher);

        vm.SwarmUrl = "http://127.0.0.1:7801";
        vm.OpenSwarmUiCommand.Execute(null);

        Assert.NotNull(process.LastStartInfo);
        Assert.Equal(vm.SwarmUrl, process.LastStartInfo?.FileName);
    }

    [Fact]
    public void QueueRecomputePrompt_UsesDispatcher()
    {
        var dispatcher = new ImmediateDispatcherService();
        var fileSystem = new FakeFileSystem();
        var appDataStore = new FakeAppDataStore("C:\\Temp\\PromptLoomTests");
        var fileReader = new FakeWildcardFileReader(new Dictionary<string, List<string>>
        {
            ["C:\\Temp\\PromptLoomTests\\Categories\\Body\\Head.txt"] = new() { "seed" }
        });
        var vm = new MainViewModel(
            fileSystem: fileSystem,
            appDataStore: appDataStore,
            fileReader: fileReader,
            dispatcher: dispatcher,
            searchViewModel: BuildSearchViewModel());

        vm.SearchViewModel.SelectedFiles.Add(new TagFileInfo("C:\\Temp\\PromptLoomTests\\Categories\\Body\\Head.txt", "Head.txt", 1));

        Assert.Equal("seed", vm.PromptText);
    }

    private static SearchViewModel BuildSearchViewModel()
    {
        var tagIndexer = new FakeTagIndexer();
        var tagSearchService = new FakeTagSearchService();
        return new SearchViewModel(tagIndexer, tagSearchService, new FakeErrorReporter());
    }
}

internal sealed class FakeWildcardFileReader : IWildcardFileReader
{
    private readonly Dictionary<string, List<string>> _entries;

    public FakeWildcardFileReader(Dictionary<string, List<string>> entries)
    {
        _entries = entries;
    }

    public List<string> LoadWildcardFile(string path)
        => _entries.TryGetValue(path, out var entries) ? entries : new List<string>();
}

internal sealed class FixedRandomSource : IRandomSource
{
    public Random Create(int? seed) => new FixedRandom();
}

internal sealed class FixedRandom : Random
{
    public override int Next(int maxValue) => 0;
}

internal sealed class FakeTagIndexer : ITagIndexer
{
    public Task<TagIndexSyncResult> SyncAsync(CancellationToken ct = default, IProgress<TagIndexProgress>? progress = null)
        => Task.FromResult(new TagIndexSyncResult());
}

internal sealed class FakeTagSearchService : ITagSearchService
{
    public Task<IReadOnlyList<string>> SuggestTagsAsync(string query, int limit, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());

    public Task<IReadOnlyList<TagFileInfo>> SearchFilesAsync(IReadOnlyCollection<string> tags, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TagFileInfo>>(Array.Empty<TagFileInfo>());

    public Task<IReadOnlyDictionary<string, int>> CountTagReferencesAsync(
        IReadOnlyCollection<string> tags,
        IReadOnlyCollection<string> filePaths,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());

    public Task<IReadOnlyDictionary<string, int>> CountTagReferencesAllFilesAsync(
        IReadOnlyCollection<string> tags,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, int>>(new Dictionary<string, int>());

    public Task<IReadOnlyDictionary<string, string>> GetCategoryColorsAsync(
        IReadOnlyCollection<string> folderPaths,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

    public Task<IReadOnlyDictionary<string, string>> GetTagColorsAsync(
        IReadOnlyCollection<string> tags,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>());

    public Task<IReadOnlyDictionary<string, IReadOnlyList<string>>> GetTopTagsByContentAsync(
        IReadOnlyCollection<string> filePaths,
        int limit,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyDictionary<string, IReadOnlyList<string>>>(
            new Dictionary<string, IReadOnlyList<string>>());

    public Task<IReadOnlyList<TagRelatedInfo>> GetRelatedTagsAsync(
        IReadOnlyCollection<string> selectedTags,
        IReadOnlyCollection<string> filePaths,
        int limit,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TagRelatedInfo>>(Array.Empty<TagRelatedInfo>());

    public string NormalizeTag(string tag) => tag.Trim().ToLowerInvariant();
}
