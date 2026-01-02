// CHANGE LOG
// - 2026-03-02 | Request: File relevance | Add relevance percent for search results.
// - 2026-01-02 | Request: Tag count scope | Add global tag count queries for suggestions.
// - 2026-01-02 | Request: Tag counts | Add tag reference counts and match totals.
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
public sealed record TagFileInfo(string Path, string FileName, int MatchCount)
{
    /// <summary>
    /// File label that includes the match count prefix.
    /// </summary>
    public string DisplayName => $"({MatchCount}) {FileName}";

    /// <summary>
    /// Relative relevance for the current result set (0-100).
    /// </summary>
    public int RelevancePercent { get; init; }
}

/// <summary>
/// Related tag result with a relevance percentage.
/// </summary>
public sealed record TagRelatedInfo(string Name, int RelevancePercent);

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

    /// <summary>
    /// Returns per-tag file reference counts limited to the provided file list.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> CountTagReferencesAsync(
        IReadOnlyCollection<string> tags,
        IReadOnlyCollection<string> filePaths,
        CancellationToken ct = default);

    /// <summary>
    /// Returns per-tag file reference counts across all indexed files.
    /// </summary>
    Task<IReadOnlyDictionary<string, int>> CountTagReferencesAllFilesAsync(
        IReadOnlyCollection<string> tags,
        CancellationToken ct = default);

    /// <summary>
    /// Returns related tags ordered by relevance for the provided file list.
    /// </summary>
    Task<IReadOnlyList<TagRelatedInfo>> GetRelatedTagsAsync(
        IReadOnlyCollection<string> selectedTags,
        IReadOnlyCollection<string> filePaths,
        int limit,
        CancellationToken ct = default);
}

/// <summary>
/// Default SQLite-backed tag search service.
/// </summary>
public sealed class TagSearchService : ITagSearchService
{
    private const double FileNameWeight = 3.0;
    private const double PathWeight = 2.0;
    private const double ContentWeight = 1.0;

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
        var sql = "SELECT f.Path, f.FileName, SUM(ft.OccurrenceCount) AS MatchCount " +
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
            results.Add(new TagFileInfo(reader.GetString(0), reader.GetString(1), reader.GetInt32(2)));

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, int>> CountTagReferencesAsync(
        IReadOnlyCollection<string> tags,
        IReadOnlyCollection<string> filePaths,
        CancellationToken ct = default)
    {
        if (tags.Count == 0 || filePaths.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var normalizedTags = tags
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedTags.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await _tagIndexStore.InitializeAsync(ct);

        await using var connection = _tagIndexStore.CreateConnection();
        await connection.OpenAsync(ct);

        var tagParams = normalizedTags.Select((_, index) => $"$tag{index}").ToList();
        var pathParams = filePaths.Select((_, index) => $"$path{index}").ToList();
        var sql = "SELECT t.Name, COUNT(DISTINCT ft.FileId) AS FileCount " +
                  "FROM FileTags ft " +
                  "JOIN Tags t ON t.Id = ft.TagId " +
                  "JOIN Files f ON f.Id = ft.FileId " +
                  $"WHERE t.Name IN ({string.Join(",", tagParams)}) " +
                  $"AND f.Path IN ({string.Join(",", pathParams)}) " +
                  "GROUP BY t.Name;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < normalizedTags.Count; i++)
            command.Parameters.AddWithValue(tagParams[i], normalizedTags[i]);
        var pathIndex = 0;
        foreach (var path in filePaths)
        {
            command.Parameters.AddWithValue(pathParams[pathIndex], path);
            pathIndex++;
        }

        var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results[reader.GetString(0)] = reader.GetInt32(1);

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, int>> CountTagReferencesAllFilesAsync(
        IReadOnlyCollection<string> tags,
        CancellationToken ct = default)
    {
        if (tags.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var normalizedTags = tags
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedTags.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await _tagIndexStore.InitializeAsync(ct);

        await using var connection = _tagIndexStore.CreateConnection();
        await connection.OpenAsync(ct);

        var tagParams = normalizedTags.Select((_, index) => $"$tag{index}").ToList();
        var sql = "SELECT t.Name, COUNT(DISTINCT ft.FileId) AS FileCount " +
                  "FROM FileTags ft " +
                  "JOIN Tags t ON t.Id = ft.TagId " +
                  $"WHERE t.Name IN ({string.Join(",", tagParams)}) " +
                  "GROUP BY t.Name;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < normalizedTags.Count; i++)
            command.Parameters.AddWithValue(tagParams[i], normalizedTags[i]);

        var results = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results[reader.GetString(0)] = reader.GetInt32(1);

        return results;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TagRelatedInfo>> GetRelatedTagsAsync(
        IReadOnlyCollection<string> selectedTags,
        IReadOnlyCollection<string> filePaths,
        int limit,
        CancellationToken ct = default)
    {
        if (selectedTags.Count == 0 || filePaths.Count == 0 || limit <= 0)
            return Array.Empty<TagRelatedInfo>();

        var normalizedTags = selectedTags
            .Select(tag => tag.Trim().ToLowerInvariant())
            .Where(tag => tag.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedTags.Count == 0)
            return Array.Empty<TagRelatedInfo>();

        await _tagIndexStore.InitializeAsync(ct);

        await using var connection = _tagIndexStore.CreateConnection();
        await connection.OpenAsync(ct);

        var tagParams = normalizedTags.Select((_, index) => $"$tag{index}").ToList();
        var pathParams = filePaths.Select((_, index) => $"$path{index}").ToList();
        var sql = "SELECT t.Name, " +
                  "(SUM(ft.FileNameCount) * $fileNameWeight) + " +
                  "(SUM(ft.PathCount) * $pathWeight) + " +
                  "(SUM(ft.ContentCount) * $contentWeight) AS Score " +
                  "FROM FileTags ft " +
                  "JOIN Tags t ON t.Id = ft.TagId " +
                  "JOIN Files f ON f.Id = ft.FileId " +
                  $"WHERE t.Name NOT IN ({string.Join(",", tagParams)}) " +
                  $"AND f.Path IN ({string.Join(",", pathParams)}) " +
                  "GROUP BY t.Name " +
                  "ORDER BY Score DESC, t.Name COLLATE NOCASE " +
                  "LIMIT $limit;";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        for (var i = 0; i < normalizedTags.Count; i++)
            command.Parameters.AddWithValue(tagParams[i], normalizedTags[i]);
        var pathIndex = 0;
        foreach (var path in filePaths)
        {
            command.Parameters.AddWithValue(pathParams[pathIndex], path);
            pathIndex++;
        }
        command.Parameters.AddWithValue("$fileNameWeight", FileNameWeight);
        command.Parameters.AddWithValue("$pathWeight", PathWeight);
        command.Parameters.AddWithValue("$contentWeight", ContentWeight);
        command.Parameters.AddWithValue("$limit", limit);

        var scored = new List<(string Name, double Score)>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            scored.Add((reader.GetString(0), reader.GetDouble(1)));

        if (scored.Count == 0)
            return Array.Empty<TagRelatedInfo>();

        var maxScore = scored.Max(entry => entry.Score);
        if (maxScore <= 0)
            return Array.Empty<TagRelatedInfo>();

        return scored
            .Select(entry => new TagRelatedInfo(entry.Name, ToPercent(entry.Score, maxScore)))
            .ToList();
    }

    private static int ToPercent(double score, double maxScore)
    {
        if (maxScore <= 0)
            return 0;

        var percent = (int)Math.Round(score / maxScore * 100, MidpointRounding.AwayFromZero);
        return Math.Clamp(percent, 0, 100);
    }
}
