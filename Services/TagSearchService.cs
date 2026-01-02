// CHANGE LOG
// - 2026-01-02 | Request: Tag search queries | Add FTS5-backed tag search service.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace PromptLoom.Services;

/// <summary>
/// File-level search result for tag queries.
/// </summary>
public sealed record TagFileInfo(string Path, string FileName);

/// <summary>
/// Abstraction for tag search queries and suggestions.
/// </summary>
public interface ITagSearchService
{
    /// <summary>
    /// Returns tag suggestions for the supplied query.
    /// </summary>
    Task<IReadOnlyList<string>> SuggestTagsAsync(string query, int limit, CancellationToken ct = default);

    /// <summary>
    /// Returns files that match all provided tags.
    /// </summary>
    Task<IReadOnlyList<TagFileInfo>> SearchFilesAsync(IReadOnlyCollection<string> tags, CancellationToken ct = default);
}

/// <summary>
/// Default SQLite-backed tag search service.
/// </summary>
public sealed class TagSearchService : ITagSearchService
{
    private readonly ITagIndexStore _tagIndexStore;
    private readonly IStopWordsStore _stopWordsStore;
    private readonly ITagTokenizer _tokenizer;

    /// <summary>
    /// Creates a new tag search service.
    /// </summary>
    public TagSearchService(ITagIndexStore tagIndexStore, IStopWordsStore stopWordsStore, ITagTokenizer tokenizer)
    {
        _tagIndexStore = tagIndexStore;
        _stopWordsStore = stopWordsStore;
        _tokenizer = tokenizer;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> SuggestTagsAsync(string query, int limit, CancellationToken ct = default)
    {
        if (limit <= 0)
            return Array.Empty<string>();

        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<string>();

        await _tagIndexStore.InitializeAsync(ct);
        var stopWords = _stopWordsStore.LoadOrCreate();
        var tokens = _tokenizer.Tokenize(new[] { query }, stopWords).Keys.ToList();
        if (tokens.Count == 0)
            return Array.Empty<string>();

        var ftsQuery = string.Join(" AND ", tokens.Select(token => $"{token}*"));
        await using var connection = _tagIndexStore.CreateConnection();
        await connection.OpenAsync(ct);

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Name FROM TagFts WHERE TagFts MATCH $query ORDER BY Name LIMIT $limit;";
        command.Parameters.AddWithValue("$query", ftsQuery);
        command.Parameters.AddWithValue("$limit", limit);

        var results = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(reader.GetString(0));

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagFileInfo>> SearchFilesAsync(IReadOnlyCollection<string> tags, CancellationToken ct = default)
    {
        if (tags.Count == 0)
            return Array.Empty<TagFileInfo>();

        var normalizedTags = tags
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedTags.Count == 0)
            return Array.Empty<TagFileInfo>();

        await _tagIndexStore.InitializeAsync(ct);

        await using var connection = _tagIndexStore.CreateConnection();
        await connection.OpenAsync(ct);

        var paramNames = normalizedTags.Select((_, index) => $"$tag{index}").ToList();
        var sql = "SELECT f.Path, f.FileName " +
                  "FROM Files f " +
                  "JOIN FileTags ft ON ft.FileId = f.Id " +
                  "JOIN Tags t ON t.Id = ft.TagId " +
                  $"WHERE t.Name IN ({string.Join(",", paramNames)}) " +
                  "GROUP BY f.Id " +
                  "HAVING COUNT(DISTINCT t.Name) = $tagCount " +
                  "ORDER BY f.FileName COLLATE NOCASE;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < normalizedTags.Count; i++)
            command.Parameters.AddWithValue(paramNames[i], normalizedTags[i]);
        command.Parameters.AddWithValue("$tagCount", normalizedTags.Count);

        var results = new List<TagFileInfo>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(new TagFileInfo(reader.GetString(0), reader.GetString(1)));

        return results;
    }
}
