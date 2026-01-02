// CHANGE LOG
// - 2026-01-02 | Fix: Content tag reindex | Force a full rescan when tag index version changes.
// - 2026-01-02 | Request: Content tag indexing | Include file contents in tag counts.
// - 2026-01-02 | Request: Tag search indexing | Add tag indexing pipeline with DB sync and counts.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace PromptLoom.Services;

/// <summary>
/// Results for a tag index synchronization pass.
/// </summary>
public sealed class TagIndexSyncResult
{
    public int AddedFiles { get; init; }
    public int UpdatedFiles { get; init; }
    public int RemovedFiles { get; init; }
    public int TotalFiles { get; init; }
    public int TotalTags { get; init; }
}

/// <summary>
/// Abstraction for synchronizing the tag index.
/// </summary>
public interface ITagIndexer
{
    /// <summary>
    /// Synchronizes the tag index with the Categories directory.
    /// </summary>
    Task<TagIndexSyncResult> SyncAsync(CancellationToken ct = default);
}

/// <summary>
/// Default tag indexer that scans Categories and syncs SQLite storage.
/// </summary>
public sealed class TagIndexer : ITagIndexer
{
    private const int CurrentIndexVersion = 2;
    private static readonly SemaphoreSlim IndexLock = new(1, 1);

    private readonly ITagIndexStore _tagIndexStore;
    private readonly IStopWordsStore _stopWordsStore;
    private readonly ITagTokenizer _tokenizer;
    private readonly IAppDataStore _appDataStore;
    private readonly IFileSystem _fileSystem;
    private readonly IClock _clock;

    /// <summary>
    /// Creates a new tag indexer.
    /// </summary>
    public TagIndexer(
        ITagIndexStore tagIndexStore,
        IStopWordsStore stopWordsStore,
        ITagTokenizer tokenizer,
        IAppDataStore appDataStore,
        IFileSystem fileSystem,
        IClock clock)
    {
        _tagIndexStore = tagIndexStore;
        _stopWordsStore = stopWordsStore;
        _tokenizer = tokenizer;
        _appDataStore = appDataStore;
        _fileSystem = fileSystem;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<TagIndexSyncResult> SyncAsync(CancellationToken ct = default)
    {
        await IndexLock.WaitAsync(ct);
        try
        {
            await _tagIndexStore.InitializeAsync(ct);

            var stopWords = _stopWordsStore.LoadOrCreate();
            var categoriesRoot = _appDataStore.CategoriesDir;
            var snapshots = BuildSnapshots(categoriesRoot);

            await using var connection = _tagIndexStore.CreateConnection();
            await connection.OpenAsync(ct);
            using var transaction = connection.BeginTransaction();

            await ExecuteNonQueryAsync(connection, transaction, "PRAGMA foreign_keys = ON;", ct);

            var storedIndexVersion = await LoadIndexVersionAsync(connection, transaction, ct);
            var forceResync = storedIndexVersion < CurrentIndexVersion;

            var existingFiles = await LoadExistingFilesAsync(connection, transaction, ct);
            var removedPaths = existingFiles.Keys.Except(snapshots.Keys, StringComparer.OrdinalIgnoreCase).ToList();

            var addedFiles = 0;
            var updatedFiles = 0;
            var removedFiles = removedPaths.Count;

            await using var deleteFileCommand = CreateCommand(
                connection,
                transaction,
                "DELETE FROM Files WHERE Path = $path;",
                "$path",
                out var deletePathParam);

            foreach (var path in removedPaths)
            {
                deletePathParam.Value = path;
                await deleteFileCommand.ExecuteNonQueryAsync(ct);
            }

            await using var upsertFileCommand = CreateCommand(
                connection,
                transaction,
                "INSERT INTO Files (Path, FileName, LastWriteTicks) VALUES ($path, $fileName, $lastWriteTicks) " +
                "ON CONFLICT(Path) DO UPDATE SET FileName = $fileName, LastWriteTicks = $lastWriteTicks;",
                "$path",
                "$fileName",
                "$lastWriteTicks",
                out var upsertFilePathParam,
                out var upsertFileNameParam,
                out var upsertLastWriteParam);

            await using var selectFileIdCommand = CreateCommand(
                connection,
                transaction,
                "SELECT Id FROM Files WHERE Path = $path;",
                "$path",
                out var selectFileIdPathParam);

            await using var deleteFileTagsCommand = CreateCommand(
                connection,
                transaction,
                "DELETE FROM FileTags WHERE FileId = $fileId;",
                "$fileId",
                out var deleteFileTagsParam);

            await using var insertTagCommand = CreateCommand(
                connection,
                transaction,
                "INSERT OR IGNORE INTO Tags (Name) VALUES ($name);",
                "$name",
                out var insertTagNameParam);

            await using var selectTagIdCommand = CreateCommand(
                connection,
                transaction,
                "SELECT Id FROM Tags WHERE Name = $name;",
                "$name",
                out var selectTagNameParam);

            await using var insertFileTagCommand = CreateCommand(
                connection,
                transaction,
                "INSERT INTO FileTags (FileId, TagId, OccurrenceCount) VALUES ($fileId, $tagId, $count);",
                "$fileId",
                "$tagId",
                "$count",
                out var insertFileIdParam,
                out var insertTagIdParam,
                out var insertCountParam);

            var tagIdCache = new Dictionary<string, long>(StringComparer.Ordinal);
            foreach (var snapshot in snapshots.Values)
            {
                var hasExisting = existingFiles.TryGetValue(snapshot.Path, out var existing);
                var isNew = forceResync || !hasExisting || existing == null || existing.LastWriteTicks != snapshot.LastWriteTicks;

                if (!isNew)
                    continue;

                if (!hasExisting || existing == null)
                    addedFiles++;
                else
                    updatedFiles++;

                upsertFilePathParam.Value = snapshot.Path;
                upsertFileNameParam.Value = snapshot.FileName;
                upsertLastWriteParam.Value = snapshot.LastWriteTicks;
                await upsertFileCommand.ExecuteNonQueryAsync(ct);

                selectFileIdPathParam.Value = snapshot.Path;
                var fileIdResult = await selectFileIdCommand.ExecuteScalarAsync(ct);
                if (fileIdResult == null)
                    throw new InvalidOperationException($"Failed to resolve file id for {snapshot.Path}.");

                var fileId = Convert.ToInt64(fileIdResult);
                deleteFileTagsParam.Value = fileId;
                await deleteFileTagsCommand.ExecuteNonQueryAsync(ct);

                var tagCounts = BuildTagCounts(categoriesRoot, snapshot.Path, stopWords);
                foreach (var entry in tagCounts)
                {
                    var tagId = await GetOrCreateTagIdAsync(
                        entry.Key,
                        insertTagCommand,
                        insertTagNameParam,
                        selectTagIdCommand,
                        selectTagNameParam,
                        tagIdCache,
                        ct);

                    insertFileIdParam.Value = fileId;
                    insertTagIdParam.Value = tagId;
                    insertCountParam.Value = entry.Value;
                    await insertFileTagCommand.ExecuteNonQueryAsync(ct);
                }
            }

            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "DELETE FROM Tags WHERE Id NOT IN (SELECT DISTINCT TagId FROM FileTags);",
                ct);

            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "UPDATE IndexState SET LastScanTicks = $ticks, CategoriesRoot = $categoriesRoot, IndexVersion = $indexVersion WHERE Id = 1;",
                ct,
                ("$ticks", _clock.Now.Ticks),
                ("$categoriesRoot", categoriesRoot),
                ("$indexVersion", CurrentIndexVersion));

            await ExecuteNonQueryAsync(connection, transaction, "INSERT INTO TagFts(TagFts) VALUES('rebuild');", ct);

            var totalFiles = (int)await ExecuteScalarAsync(connection, transaction, "SELECT COUNT(*) FROM Files;", ct);
            var totalTags = (int)await ExecuteScalarAsync(connection, transaction, "SELECT COUNT(*) FROM Tags;", ct);

            transaction.Commit();

            return new TagIndexSyncResult
            {
                AddedFiles = addedFiles,
                UpdatedFiles = updatedFiles,
                RemovedFiles = removedFiles,
                TotalFiles = totalFiles,
                TotalTags = totalTags
            };
        }
        finally
        {
            IndexLock.Release();
        }
    }

    private Dictionary<string, FileSnapshot> BuildSnapshots(string categoriesRoot)
    {
        var snapshots = new Dictionary<string, FileSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (!_fileSystem.DirectoryExists(categoriesRoot))
            return snapshots;

        foreach (var path in _fileSystem.EnumerateFiles(categoriesRoot, "*.txt", SearchOption.AllDirectories))
        {
            var lastWriteTicks = File.GetLastWriteTimeUtc(path).Ticks;
            var fileName = Path.GetFileNameWithoutExtension(path);
            snapshots[path] = new FileSnapshot(path, fileName, lastWriteTicks);
        }

        return snapshots;
    }

    private IReadOnlyDictionary<string, int> BuildTagCounts(string categoriesRoot, string filePath, IReadOnlySet<string> stopWords)
    {
        var relativePath = Path.GetRelativePath(categoriesRoot, filePath);
        var segments = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        var tokenSegments = new List<string>(segments.Length);
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (i == segments.Length - 1)
                segment = Path.GetFileNameWithoutExtension(segment);

            tokenSegments.Add(segment);
        }

        var counts = new Dictionary<string, int>(_tokenizer.Tokenize(tokenSegments, stopWords), StringComparer.Ordinal);
        var contentCounts = TokenizeFileContents(filePath, stopWords);
        foreach (var entry in contentCounts)
        {
            if (counts.TryGetValue(entry.Key, out var existing))
                counts[entry.Key] = existing + entry.Value;
            else
                counts[entry.Key] = entry.Value;
        }

        return counts;
    }

    private IReadOnlyDictionary<string, int> TokenizeFileContents(string filePath, IReadOnlySet<string> stopWords)
    {
        try
        {
            var contents = _fileSystem.ReadAllText(filePath);
            if (string.IsNullOrWhiteSpace(contents))
                return new Dictionary<string, int>(StringComparer.Ordinal);

            return _tokenizer.Tokenize(new[] { contents }, stopWords);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
        {
            return new Dictionary<string, int>(StringComparer.Ordinal);
        }
    }

    private static async Task<Dictionary<string, FileEntry>> LoadExistingFilesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id, Path, LastWriteTicks FROM Files;";
        await using var reader = await command.ExecuteReaderAsync(ct);

        var results = new Dictionary<string, FileEntry>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(ct))
        {
            var id = reader.GetInt64(0);
            var path = reader.GetString(1);
            var ticks = reader.GetInt64(2);
            results[path] = new FileEntry(id, ticks);
        }

        return results;
    }

    private static async Task<int> LoadIndexVersionAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT IndexVersion FROM IndexState WHERE Id = 1;";
        var result = await command.ExecuteScalarAsync(ct);
        return result == null ? 0 : Convert.ToInt32(result);
    }

    private static async Task<long> GetOrCreateTagIdAsync(
        string tag,
        SqliteCommand insertTagCommand,
        SqliteParameter insertTagNameParam,
        SqliteCommand selectTagIdCommand,
        SqliteParameter selectTagNameParam,
        IDictionary<string, long> cache,
        CancellationToken ct)
    {
        if (cache.TryGetValue(tag, out var cachedId))
            return cachedId;

        insertTagNameParam.Value = tag;
        await insertTagCommand.ExecuteNonQueryAsync(ct);

        selectTagNameParam.Value = tag;
        var result = await selectTagIdCommand.ExecuteScalarAsync(ct);
        if (result == null)
            throw new InvalidOperationException($"Failed to resolve tag id for {tag}.");

        var id = Convert.ToInt64(result);
        cache[tag] = id;
        return id;
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        string param1Name,
        out SqliteParameter param1)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        param1 = command.CreateParameter();
        param1.ParameterName = param1Name;
        command.Parameters.Add(param1);
        return command;
    }

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        string param1Name,
        string param2Name,
        string param3Name,
        out SqliteParameter param1,
        out SqliteParameter param2,
        out SqliteParameter param3)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        param1 = command.CreateParameter();
        param2 = command.CreateParameter();
        param3 = command.CreateParameter();
        param1.ParameterName = param1Name;
        param2.ParameterName = param2Name;
        param3.ParameterName = param3Name;
        command.Parameters.Add(param1);
        command.Parameters.Add(param2);
        command.Parameters.Add(param3);
        return command;
    }

    private static async Task ExecuteNonQueryAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken ct,
        params (string Name, object Value)[] parameters)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        foreach (var parameter in parameters)
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long> ExecuteScalarAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(ct);
        return result == null ? 0 : Convert.ToInt64(result);
    }

    private sealed record FileSnapshot(string Path, string FileName, long LastWriteTicks);

    private sealed record FileEntry(long Id, long LastWriteTicks);
}
