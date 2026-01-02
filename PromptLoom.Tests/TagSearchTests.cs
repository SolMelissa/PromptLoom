// CHANGE LOG
// - 2026-01-02 | Fix: Forced reindex | Verify older index versions trigger a rescan.
// - 2026-01-02 | Request: Content tag indexing test | Assert content-only tags are indexed.
// - 2026-01-02 | Request: Content tag indexing | Add file-content tag coverage.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using PromptLoom.Services;
using PromptLoom.Tests.Fakes;
using Xunit;

namespace PromptLoom.Tests;

public sealed class TagSearchTests
{
    [Fact]
    public void TagTokenizer_TracksCountsAndSkipsStopWords()
    {
        var tokenizer = new TagTokenizer();
        var stopWords = new HashSet<string>(StringComparer.Ordinal) { "and" };
        var segments = new[] { "Body", "Head and Shoulders", "Old Brunette", "Head" };

        var tokens = tokenizer.Tokenize(segments, stopWords);

        Assert.Equal(1, tokens["body"]);
        Assert.Equal(2, tokens["head"]);
        Assert.Equal(1, tokens["shoulders"]);
        Assert.Equal(1, tokens["old"]);
        Assert.Equal(1, tokens["brunette"]);
        Assert.False(tokens.ContainsKey("and"));
    }

    [Fact]
    public async Task TagIndexer_SyncsAddsAndRemovesEntries()
    {
        var rootDir = Path.Combine(AppContext.BaseDirectory, "TagIndexTests", Guid.NewGuid().ToString("N"));
        var categoriesDir = Path.Combine(rootDir, "Categories");
        var filePath = Path.Combine(categoriesDir, "Body", "Head and Shoulders", "Old Brunette.txt");

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "old old seed");

        try
        {
            var fileSystem = new FileSystem();
            var appDataStore = new FakeAppDataStore(rootDir);
            var tagIndexStore = new TagIndexStore(fileSystem, appDataStore);
            var stopWordsStore = new StopWordsStore(fileSystem, appDataStore);
            var tokenizer = new TagTokenizer();
            var tagIndexer = new TagIndexer(tagIndexStore, stopWordsStore, tokenizer, appDataStore, fileSystem, new SystemClock());

            var result = await tagIndexer.SyncAsync();

            Assert.Equal(1, result.TotalFiles);
            Assert.True(File.Exists(Path.Combine(rootDir, "DBs", "Tags.db")));

            var tagCounts = await LoadTagCountsAsync(tagIndexStore, filePath);
            Assert.Equal(1, tagCounts["body"]);
            Assert.Equal(1, tagCounts["head"]);
            Assert.Equal(1, tagCounts["shoulders"]);
            Assert.Equal(3, tagCounts["old"]);
            Assert.Equal(1, tagCounts["brunette"]);
            Assert.Equal(1, tagCounts["seed"]);
            Assert.False(tagCounts.ContainsKey("and"));

            File.Delete(filePath);
            var resultAfterDelete = await tagIndexer.SyncAsync();
            Assert.Equal(0, resultAfterDelete.TotalFiles);
        }
        finally
        {
            CleanupRoot(rootDir);
        }
    }

    [Fact]
    public async Task TagSearchService_UsesAndMatchingAndFts()
    {
        var rootDir = Path.Combine(AppContext.BaseDirectory, "TagSearchTests", Guid.NewGuid().ToString("N"));
        var categoriesDir = Path.Combine(rootDir, "Categories");
        var filePath = Path.Combine(categoriesDir, "Body", "Head and Shoulders", "Old Brunette.txt");

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "seed seed");

        try
        {
            var fileSystem = new FileSystem();
            var appDataStore = new FakeAppDataStore(rootDir);
            var tagIndexStore = new TagIndexStore(fileSystem, appDataStore);
            var stopWordsStore = new StopWordsStore(fileSystem, appDataStore);
            var tokenizer = new TagTokenizer();
            var tagIndexer = new TagIndexer(tagIndexStore, stopWordsStore, tokenizer, appDataStore, fileSystem, new SystemClock());
            var searchService = new TagSearchService(tagIndexStore, stopWordsStore, tokenizer);

            await tagIndexer.SyncAsync();

            var matches = await searchService.SearchFilesAsync(new[] { "body", "head" });
            Assert.Single(matches);
            Assert.Equal(filePath, matches[0].Path);

            var missing = await searchService.SearchFilesAsync(new[] { "body", "missing" });
            Assert.Empty(missing);

            var contentMatches = await searchService.SearchFilesAsync(new[] { "seed" });
            Assert.Single(contentMatches);
            Assert.Equal(filePath, contentMatches[0].Path);

            var suggestions = await searchService.SuggestTagsAsync("hea", 5);
            Assert.Contains("head", suggestions);
        }
        finally
        {
            CleanupRoot(rootDir);
        }
    }

    [Fact]
    public async Task TagIndexer_ReadsFileContentsForAdditionalTags()
    {
        var rootDir = Path.Combine(AppContext.BaseDirectory, "TagContentTests", Guid.NewGuid().ToString("N"));
        var categoriesDir = Path.Combine(rootDir, "Categories");
        var filePath = Path.Combine(categoriesDir, "Body", "Head", "Plain.txt");

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "zebra zebra");

        try
        {
            var fileSystem = new FileSystem();
            var appDataStore = new FakeAppDataStore(rootDir);
            var tagIndexStore = new TagIndexStore(fileSystem, appDataStore);
            var stopWordsStore = new StopWordsStore(fileSystem, appDataStore);
            var tokenizer = new TagTokenizer();
            var tagIndexer = new TagIndexer(tagIndexStore, stopWordsStore, tokenizer, appDataStore, fileSystem, new SystemClock());

            await tagIndexer.SyncAsync();

            var tagCounts = await LoadTagCountsAsync(tagIndexStore, filePath);
            Assert.Equal(2, tagCounts["zebra"]);
        }
        finally
        {
            CleanupRoot(rootDir);
        }
    }

    [Fact]
    public async Task TagIndexer_ForcesReindexWhenIndexVersionChanges()
    {
        var rootDir = Path.Combine(AppContext.BaseDirectory, "TagReindexTests", Guid.NewGuid().ToString("N"));
        var categoriesDir = Path.Combine(rootDir, "Categories");
        var filePath = Path.Combine(categoriesDir, "Body", "Head", "Plain.txt");

        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, "zebra");

        try
        {
            var fileSystem = new FileSystem();
            var appDataStore = new FakeAppDataStore(rootDir);
            var tagIndexStore = new TagIndexStore(fileSystem, appDataStore);
            var stopWordsStore = new StopWordsStore(fileSystem, appDataStore);
            var tokenizer = new TagTokenizer();
            var tagIndexer = new TagIndexer(tagIndexStore, stopWordsStore, tokenizer, appDataStore, fileSystem, new SystemClock());

            await tagIndexer.SyncAsync();

            await SetIndexVersionAsync(tagIndexStore, 1);
            await ClearTagsAsync(tagIndexStore);

            await tagIndexer.SyncAsync();

            var tagCounts = await LoadTagCountsAsync(tagIndexStore, filePath);
            Assert.Equal(1, tagCounts["zebra"]);
        }
        finally
        {
            CleanupRoot(rootDir);
        }
    }

    private static async Task<Dictionary<string, int>> LoadTagCountsAsync(ITagIndexStore store, string filePath)
    {
        await using var connection = store.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT t.Name, ft.OccurrenceCount " +
                              "FROM Tags t " +
                              "JOIN FileTags ft ON ft.TagId = t.Id " +
                              "JOIN Files f ON f.Id = ft.FileId " +
                              "WHERE f.Path = $path;";
        command.Parameters.AddWithValue("$path", filePath);

        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            counts[reader.GetString(0)] = reader.GetInt32(1);

        return counts;
    }

    private static void CleanupRoot(string rootDir)
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(rootDir))
            Directory.Delete(rootDir, true);
    }

    private static async Task ClearTagsAsync(ITagIndexStore store)
    {
        await using var connection = store.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM FileTags; DELETE FROM Tags;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetIndexVersionAsync(ITagIndexStore store, int version)
    {
        await using var connection = store.CreateConnection();
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE IndexState SET IndexVersion = $version WHERE Id = 1;";
        command.Parameters.AddWithValue("$version", version);
        await command.ExecuteNonQueryAsync();
    }
}
