// CHANGE LOG
// - 2026-01-12 | Fix: Tag color cleanup | Avoid large parameter lists by deleting with Tags table.
// - 2026-01-12 | Request: Tag co-occurrence | Limit co-occurrence to filename/path tags only.
// - 2026-01-12 | Request: Tag color progress | Add heartbeat reporting while loading co-occurrences.
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public int TotalCategoryColors { get; init; }
}

/// <summary>
/// Progress updates during tag indexing.
/// </summary>
public sealed record TagIndexProgress(string Stage, int Processed, int Total)
{
    /// <summary>
    /// Percentage complete when total is known, otherwise 0.
    /// </summary>
    public int Percent => Total <= 0 ? 0 : (int)Math.Round(Processed * 100d / Total);
}

/// <summary>
/// Abstraction for synchronizing the tag index.
/// </summary>
public interface ITagIndexer
{
    /// <summary>
    /// Synchronizes the tag index with the library directory.
    /// </summary>
    /// <param name="ct">Cancellation token for the sync pass.</param>
    /// <param name="progress">Optional progress updates for long-running scans.</param>
    Task<TagIndexSyncResult> SyncAsync(CancellationToken ct = default, IProgress<TagIndexProgress>? progress = null);
}

/// <summary>
/// Default tag indexer that scans the library and syncs SQLite storage.
/// </summary>
public sealed class TagIndexer : ITagIndexer
{
    private const int CurrentIndexVersion = 5; // bump to reindex now that tokens are lemmatized
    private const int TagColorMinCooccurrence = 2;
    private const int TagColorMaxIterations = 20;
    private const double TagColorMaterialRatio = 0.05;
    private const int TagColorMaterialMinDelta = 25;
    private const double TagColorSaturationMin = 0.78;
    private const double TagColorSaturationMax = 0.92;
    private const double TagColorLightnessMin = 0.46;
    private const double TagColorLightnessMax = 0.60;
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
    public async Task<TagIndexSyncResult> SyncAsync(CancellationToken ct = default, IProgress<TagIndexProgress>? progress = null)
    {
        await IndexLock.WaitAsync(ct);
        try
        {
            await _tagIndexStore.InitializeAsync(ct);

            var stopWords = _stopWordsStore.LoadOrCreate();
            var libraryRoot = _appDataStore.LibraryDir;
            progress?.Report(new TagIndexProgress("Scanning category files", 0, 0));
            var snapshots = BuildSnapshots(libraryRoot);
            var categories = ExtractCategories(libraryRoot, snapshots.Values);
            var totalSnapshots = snapshots.Count;
            progress?.Report(new TagIndexProgress("Indexing tags", 0, totalSnapshots));

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
                "INSERT INTO FileTags (FileId, TagId, OccurrenceCount, FileNameCount, PathCount, ContentCount) " +
                "VALUES ($fileId, $tagId, $count, $fileNameCount, $pathCount, $contentCount);",
                "$fileId",
                "$tagId",
                "$count",
                out var insertFileIdParam,
                out var insertTagIdParam,
                out var insertCountParam,
                "$fileNameCount",
                "$pathCount",
                "$contentCount",
                out var insertFileNameCountParam,
                out var insertPathCountParam,
                out var insertContentCountParam);

            var tagIdCache = new Dictionary<string, long>(StringComparer.Ordinal);
            var processed = 0;
            var lastPercent = -1;
            foreach (var snapshot in snapshots.Values)
            {
                processed++;
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

                var tagCounts = BuildTagCounts(libraryRoot, snapshot.Path, stopWords);
                foreach (var entry in tagCounts)
                {
                    var breakdown = entry.Value;
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
                    insertCountParam.Value = breakdown.TotalCount;
                    insertFileNameCountParam.Value = breakdown.FileNameCount;
                    insertPathCountParam.Value = breakdown.PathCount;
                    insertContentCountParam.Value = breakdown.ContentCount;
                    await insertFileTagCommand.ExecuteNonQueryAsync(ct);
                }

                if (totalSnapshots > 0)
                {
                    var percent = (int)Math.Round(processed * 100d / totalSnapshots);
                    if (percent != lastPercent && (percent % 5 == 0 || percent == 100))
                    {
                        lastPercent = percent;
                        progress?.Report(new TagIndexProgress("Indexing tags", processed, totalSnapshots));
                    }
                }
            }

            const int finalizeSteps = 7;
            var finalizeStep = 0;
            void ReportFinalize(string stage)
            {
                finalizeStep++;
                progress?.Report(new TagIndexProgress(stage, finalizeStep, finalizeSteps));
            }

            ReportFinalize("Finalizing index: pruning tags");
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "DELETE FROM Tags WHERE Id NOT IN (SELECT DISTINCT TagId FROM FileTags);",
                ct);

            ReportFinalize("Finalizing index: updating tag counts");
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "UPDATE Tags SET OccurringFileCount = " +
                "(SELECT COUNT(DISTINCT FileId) FROM FileTags WHERE TagId = Tags.Id);",
                ct);

            ReportFinalize("Finalizing index: saving index state");
            await ExecuteNonQueryAsync(
                connection,
                transaction,
                "UPDATE IndexState SET LastScanTicks = $ticks, LibraryRoot = $libraryRoot, IndexVersion = $indexVersion WHERE Id = 1;",
                ct,
                ("$ticks", _clock.Now.Ticks),
                ("$libraryRoot", libraryRoot),
                ("$indexVersion", CurrentIndexVersion));

            ReportFinalize("Finalizing index: rebuilding search index");
            await ExecuteNonQueryAsync(connection, transaction, "INSERT INTO TagFts(TagFts) VALUES('rebuild');", ct);

            ReportFinalize("Finalizing index: counting totals");
            var totalFiles = (int)await ExecuteScalarAsync(connection, transaction, "SELECT COUNT(*) FROM Files;", ct);
            var totalTags = (int)await ExecuteScalarAsync(connection, transaction, "SELECT COUNT(*) FROM Tags;", ct);

            ReportFinalize("Finalizing index: syncing colors");
            var totalCategoryColors = await SyncCategoryColorsAsync(connection, transaction, categories, ct);
            await SyncTagColorsAsync(connection, transaction, ct, progress);

            ReportFinalize("Finalizing index: committing");
            transaction.Commit();

            return new TagIndexSyncResult
            {
                AddedFiles = addedFiles,
                UpdatedFiles = updatedFiles,
                RemovedFiles = removedFiles,
                TotalFiles = totalFiles,
                TotalTags = totalTags,
                TotalCategoryColors = totalCategoryColors
            };
        }
        finally
        {
            IndexLock.Release();
        }
    }

    private Dictionary<string, FileSnapshot> BuildSnapshots(string libraryRoot)
    {
        var snapshots = new Dictionary<string, FileSnapshot>(StringComparer.OrdinalIgnoreCase);
        if (!_fileSystem.DirectoryExists(libraryRoot))
            return snapshots;

        foreach (var path in _fileSystem.EnumerateFiles(libraryRoot, "*.txt", SearchOption.AllDirectories))
        {
            var lastWriteTicks = File.GetLastWriteTimeUtc(path).Ticks;
            var fileName = Path.GetFileNameWithoutExtension(path);
            snapshots[path] = new FileSnapshot(path, fileName, lastWriteTicks);
        }

        return snapshots;
    }

    private IReadOnlyCollection<string> ExtractCategories(string libraryRoot, IEnumerable<FileSnapshot> snapshots)
    {
        var categories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_fileSystem.DirectoryExists(libraryRoot))
        {
            foreach (var dir in EnumerateAllDirectories(libraryRoot))
            {
                var relativePath = Path.GetRelativePath(libraryRoot, dir);
                if (!string.IsNullOrWhiteSpace(relativePath) && relativePath != ".")
                    categories.Add(relativePath);
            }
        }

        foreach (var snapshot in snapshots)
        {
            var relativePath = Path.GetRelativePath(libraryRoot, snapshot.Path);
            var segments = relativePath.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length > 1)
                categories.Add(segments[0]);
        }

        return categories;
    }

    private IEnumerable<string> EnumerateAllDirectories(string root)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            var current = pending.Pop();
            foreach (var dir in _fileSystem.EnumerateDirectories(current))
            {
                yield return dir;
                pending.Push(dir);
            }
        }
    }

    private static async Task<int> SyncCategoryColorsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyCollection<string> categories,
        CancellationToken ct)
    {
        var categoryList = categories.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var existing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "SELECT Category, ColorHex FROM CategoryColors;";
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                existing[reader.GetString(0)] = reader.GetString(1);
        }

        var usedColors = new HashSet<string>(existing.Values, StringComparer.OrdinalIgnoreCase);

        foreach (var category in categoryList)
        {
            if (existing.ContainsKey(category))
                continue;

            var color = GenerateUniqueColor(category, usedColors);
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = "INSERT INTO CategoryColors (Category, ColorHex) VALUES ($category, $color);";
            insertCommand.Parameters.AddWithValue("$category", category);
            insertCommand.Parameters.AddWithValue("$color", color);
            await insertCommand.ExecuteNonQueryAsync(ct);
            existing[category] = color;
        }

        if (categoryList.Count == 0)
        {
            await ExecuteNonQueryAsync(connection, transaction, "DELETE FROM CategoryColors;", ct);
        }
        else
        {
            var keepParams = categoryList.Select((_, index) => $"$cat{index}").ToList();
            var deleteSql = $"DELETE FROM CategoryColors WHERE Category NOT IN ({string.Join(",", keepParams)});";
            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText = deleteSql;
            for (var i = 0; i < keepParams.Count; i++)
                deleteCommand.Parameters.AddWithValue(keepParams[i], categoryList[i]);
            await deleteCommand.ExecuteNonQueryAsync(ct);
        }

        var totalColors = (int)await ExecuteScalarAsync(connection, transaction, "SELECT COUNT(*) FROM CategoryColors;", ct);
        return totalColors;
    }

    private static string GenerateUniqueColor(string category, HashSet<string> usedColors)
    {
        var hue = Math.Abs(GetStableHash(category)) % 360;
        const double saturation = 0.85;
        const double lightness = 0.55;
        const double goldenStep = 137.508;

        for (var i = 0; i < 360; i++)
        {
            var candidateHue = (hue + goldenStep * i) % 360d;
            var color = BuildHexColor(candidateHue, saturation, lightness);
            if (usedColors.Add(color))
                return color;
        }

        return BuildHexColor(hue, saturation, lightness);
    }

    private static int GetStableHash(string value)
    {
        unchecked
        {
            var hash = 17;
            foreach (var ch in value)
                hash = hash * 31 + ch;
            return hash;
        }
    }

    private static string BuildHexColor(double hueDegrees, double saturation, double lightness)
    {
        var c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
        var x = c * (1 - Math.Abs((hueDegrees / 60d) % 2 - 1));
        var m = lightness - c / 2;

        double r1, g1, b1;
        if (hueDegrees < 60)
            (r1, g1, b1) = (c, x, 0);
        else if (hueDegrees < 120)
            (r1, g1, b1) = (x, c, 0);
        else if (hueDegrees < 180)
            (r1, g1, b1) = (0, c, x);
        else if (hueDegrees < 240)
            (r1, g1, b1) = (0, x, c);
        else if (hueDegrees < 300)
            (r1, g1, b1) = (x, 0, c);
        else
            (r1, g1, b1) = (c, 0, x);

        var r = (int)Math.Round((r1 + m) * 255);
        var g = (int)Math.Round((g1 + m) * 255);
        var b = (int)Math.Round((b1 + m) * 255);

        r = Math.Clamp(r, 0, 255);
        g = Math.Clamp(g, 0, 255);
        b = Math.Clamp(b, 0, 255);

        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static async Task SyncTagColorsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken ct,
        IProgress<TagIndexProgress>? progress)
    {
        ReportTagColorProgress(progress, "Tag colors: loading tags");
        var tags = await LoadTagsAsync(connection, transaction, ct);
        ReportTagColorProgress(progress, "Tag colors: loading state");
        var state = await LoadTagColorStateAsync(connection, transaction, ct);
        if (tags.Count == 0)
        {
            await ExecuteNonQueryAsync(connection, transaction, "DELETE FROM TagColors;", ct);
            await SaveTagColorStateAsync(connection, transaction, 0, string.Empty, ct);
            return;
        }

        var tagHash = BuildTagHash(tags.Select(tag => tag.Name));
        var shouldRecompute = IsMaterialTagChange(state, tags.Count, tagHash);

        ReportTagColorProgress(progress, "Tag colors: loading co-occurrences");
        var edgeStopwatch = Stopwatch.StartNew();
        using var edgeHeartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var heartbeatTask = Task.Run(async () =>
        {
            while (!edgeHeartbeatCts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2), edgeHeartbeatCts.Token);
                ReportTagColorProgress(
                    progress,
                    $"Tag colors: loading co-occurrences ({edgeStopwatch.Elapsed:mm\\:ss})");
            }
        }, edgeHeartbeatCts.Token);

        Dictionary<long, List<TagEdge>> adjacency;
        try
        {
            adjacency = await LoadTagEdgesAsync(
                connection,
                transaction,
                tags,
                ct,
                rowCount =>
                    ReportTagColorProgress(progress, $"Tag colors: loading co-occurrences ({rowCount} rows)"));
        }
        finally
        {
            edgeHeartbeatCts.Cancel();
            try
            {
                await heartbeatTask;
            }
            catch (OperationCanceledException)
            {
            }
        }
        var clusterAssignments = shouldRecompute
            ? BuildTagCommunities(tags, adjacency, (iter, total) =>
                ReportTagColorProgress(
                    progress,
                    $"Tag colors: clustering ({iter}/{total})",
                    iter,
                    total))
            : await LoadExistingTagClustersAsync(connection, transaction, tags, adjacency, ct);

        var clusterIds = clusterAssignments.Values.Distinct().OrderBy(id => id).ToList();
        var clusterIdMap = clusterIds
            .Select((id, index) => (id, index))
            .ToDictionary(pair => pair.id, pair => pair.index);
        var clusterCount = Math.Max(1, clusterIdMap.Count);

        ReportTagColorProgress(progress, shouldRecompute ? "Tag colors: loading colors" : "Tag colors: reusing colors");
        var existingColors = await LoadExistingTagColorsAsync(connection, transaction, ct);
        var nowTicks = DateTime.UtcNow.Ticks;

        ReportTagColorProgress(progress, "Tag colors: saving colors", 0, tags.Count);
        var saved = 0;
        var lastPercent = -1;
        foreach (var tag in tags)
        {
            saved++;
            var clusterId = clusterAssignments[tag.Name];
            var clusterIndex = clusterIdMap[clusterId];
            if (existingColors.TryGetValue(tag.Name, out var existing) && !shouldRecompute && existing.ClusterId == clusterId)
                continue;

            var color = BuildClusterColor(clusterIndex, clusterCount, tag.Name);
            await UpsertTagColorAsync(connection, transaction, tag.Name, color, clusterId, nowTicks, ct);

            if (tags.Count > 0)
            {
                var percent = (int)Math.Round(saved * 100d / tags.Count);
                if (percent != lastPercent && (percent % 5 == 0 || percent == 100))
                {
                    lastPercent = percent;
                    ReportTagColorProgress(progress, "Tag colors: saving colors", saved, tags.Count);
                }
            }
        }

        ReportTagColorProgress(progress, "Tag colors: cleaning up");
        await DeleteMissingTagColorsAsync(connection, transaction, ct);
        ReportTagColorProgress(progress, "Tag colors: saving state");
        await SaveTagColorStateAsync(connection, transaction, tags.Count, tagHash, ct);
    }

    private static bool IsMaterialTagChange(TagColorState state, int newCount, string newHash)
    {
        if (state.LastTagCount <= 0)
            return true;

        if (!string.Equals(state.LastTagHash, newHash, StringComparison.Ordinal))
        {
            if (state.LastTagCount == newCount)
                return true;
        }

        var delta = Math.Abs(newCount - state.LastTagCount);
        var threshold = Math.Max(TagColorMaterialMinDelta, (int)Math.Ceiling(state.LastTagCount * TagColorMaterialRatio));
        return delta >= threshold;
    }

    private static async Task<List<TagInfo>> LoadTagsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken ct)
    {
        var tags = new List<TagInfo>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Id, Name FROM Tags;";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            tags.Add(new TagInfo(reader.GetInt64(0), reader.GetString(1)));
        return tags;
    }

    private static async Task<TagColorState> LoadTagColorStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT LastTagCount, LastTagHash FROM TagColorState WHERE Id = 1;";
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (await reader.ReadAsync(ct))
        {
            return new TagColorState(reader.GetInt32(0), reader.GetString(1));
        }

        return new TagColorState(0, string.Empty);
    }

    private static async Task SaveTagColorStateAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int tagCount,
        string tagHash,
        CancellationToken ct)
    {
        await ExecuteNonQueryAsync(
            connection,
            transaction,
            "UPDATE TagColorState SET LastTagCount = $count, LastTagHash = $hash, LastUpdatedTicks = $ticks WHERE Id = 1;",
            ct,
            ("$count", tagCount),
            ("$hash", tagHash),
            ("$ticks", DateTime.UtcNow.Ticks));
    }

    private static async Task<Dictionary<long, List<TagEdge>>> LoadTagEdgesAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<TagInfo> tags,
        CancellationToken ct,
        Action<int>? reportRowCount)
    {
        var lookup = tags.ToDictionary(tag => tag.Id, tag => tag.Name);
        var edges = tags.ToDictionary(tag => tag.Id, _ => new List<TagEdge>());

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "SELECT ft1.TagId, ft2.TagId, COUNT(DISTINCT ft1.FileId) AS Weight " +
            "FROM FileTags ft1 " +
            "JOIN FileTags ft2 ON ft1.FileId = ft2.FileId AND ft1.TagId < ft2.TagId " +
            "WHERE (ft1.FileNameCount + ft1.PathCount) > 0 " +
            "AND (ft2.FileNameCount + ft2.PathCount) > 0 " +
            "GROUP BY ft1.TagId, ft2.TagId " +
            "HAVING Weight >= $minWeight;";
        command.Parameters.AddWithValue("$minWeight", TagColorMinCooccurrence);

        var rowCount = 0;
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var leftId = reader.GetInt64(0);
            var rightId = reader.GetInt64(1);
            var weight = reader.GetInt32(2);
            if (!lookup.ContainsKey(leftId) || !lookup.ContainsKey(rightId))
                continue;

            edges[leftId].Add(new TagEdge(rightId, weight));
            edges[rightId].Add(new TagEdge(leftId, weight));

            rowCount++;
            if (rowCount % 5000 == 0)
                reportRowCount?.Invoke(rowCount);
        }

        return edges;
    }

    private static Dictionary<string, int> BuildTagCommunities(
        IReadOnlyList<TagInfo> tags,
        IReadOnlyDictionary<long, List<TagEdge>> edges,
        Action<int, int>? reportIteration)
    {
        var labels = tags.ToDictionary(tag => tag.Id, tag => tag.Id);
        for (var iter = 0; iter < TagColorMaxIterations; iter++)
        {
            reportIteration?.Invoke(iter + 1, TagColorMaxIterations);
            var changed = false;
            foreach (var tag in tags)
            {
                if (!edges.TryGetValue(tag.Id, out var neighbors) || neighbors.Count == 0)
                    continue;

                var scores = new Dictionary<long, int>();
                foreach (var edge in neighbors)
                {
                    if (!labels.TryGetValue(edge.TargetId, out var neighborLabel))
                        continue;

                    scores.TryGetValue(neighborLabel, out var current);
                    scores[neighborLabel] = current + edge.Weight;
                }

                if (scores.Count == 0)
                    continue;

                var bestLabel = scores
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key)
                    .First().Key;

                if (labels[tag.Id] != bestLabel)
                {
                    labels[tag.Id] = bestLabel;
                    changed = true;
                }
            }

            if (!changed)
                break;
        }

        var labelOrder = labels.Values.Distinct().OrderBy(value => value).ToList();
        var labelToCluster = labelOrder
            .Select((label, index) => (label, index))
            .ToDictionary(pair => pair.label, pair => pair.index);

        return tags.ToDictionary(tag => tag.Name, tag => labelToCluster[labels[tag.Id]], StringComparer.OrdinalIgnoreCase);
    }

    private static void ReportTagColorProgress(
        IProgress<TagIndexProgress>? progress,
        string stage,
        int processed = 0,
        int total = 0)
    {
        progress?.Report(new TagIndexProgress(stage, processed, total));
    }

    private static async Task<Dictionary<string, int>> LoadExistingTagClustersAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyList<TagInfo> tags,
        IReadOnlyDictionary<long, List<TagEdge>> edges,
        CancellationToken ct)
    {
        var clusters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var clusterMap = new Dictionary<long, int>();
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Tag, ClusterId FROM TagColors;";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            clusters[reader.GetString(0)] = reader.GetInt32(1);

        foreach (var tag in tags)
        {
            if (clusters.TryGetValue(tag.Name, out var clusterId))
            {
                clusterMap[tag.Id] = clusterId;
                continue;
            }

            clusterId = ResolveClusterForTag(tag.Id, edges, clusterMap);
            clusterMap[tag.Id] = clusterId;
            clusters[tag.Name] = clusterId;
        }

        return clusters;
    }

    private static int ResolveClusterForTag(
        long tagId,
        IReadOnlyDictionary<long, List<TagEdge>> edges,
        IReadOnlyDictionary<long, int> clusterMap)
    {
        if (!edges.TryGetValue(tagId, out var neighbors) || neighbors.Count == 0)
            return clusterMap.Count == 0 ? 0 : clusterMap.Values.Max() + 1;

        var scores = new Dictionary<int, int>();
        foreach (var edge in neighbors)
        {
            if (!clusterMap.TryGetValue(edge.TargetId, out var clusterId))
                continue;

            scores.TryGetValue(clusterId, out var current);
            scores[clusterId] = current + edge.Weight;
        }

        if (scores.Count == 0)
            return clusterMap.Count == 0 ? 0 : clusterMap.Values.Max() + 1;

        return scores
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .First().Key;
    }

    private static async Task<Dictionary<string, TagColorEntry>> LoadExistingTagColorsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken ct)
    {
        var colors = new Dictionary<string, TagColorEntry>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Tag, ColorHex, ClusterId FROM TagColors;";
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            colors[reader.GetString(0)] = new TagColorEntry(reader.GetString(1), reader.GetInt32(2));
        return colors;
    }

    private static async Task UpsertTagColorAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string tag,
        string color,
        int clusterId,
        long updatedTicks,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            "INSERT INTO TagColors (Tag, ColorHex, ClusterId, UpdatedTicks) VALUES ($tag, $color, $clusterId, $ticks) " +
            "ON CONFLICT(Tag) DO UPDATE SET ColorHex = $color, ClusterId = $clusterId, UpdatedTicks = $ticks;";
        command.Parameters.AddWithValue("$tag", tag);
        command.Parameters.AddWithValue("$color", color);
        command.Parameters.AddWithValue("$clusterId", clusterId);
        command.Parameters.AddWithValue("$ticks", updatedTicks);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task DeleteMissingTagColorsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DELETE FROM TagColors WHERE Tag NOT IN (SELECT Name FROM Tags);";
        await command.ExecuteNonQueryAsync(ct);
    }

    private static string BuildTagHash(IEnumerable<string> tags)
    {
        unchecked
        {
            var hash = 17;
            foreach (var tag in tags.OrderBy(name => name, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var ch in tag)
                    hash = hash * 31 + ch;
                hash = hash * 31 + '|';
            }

            return hash.ToString("X");
        }
    }

    private static string BuildClusterColor(int clusterIndex, int clusterCount, string tag)
    {
        var baseHue = clusterCount <= 0 ? 0 : (360d * clusterIndex / clusterCount);
        var hash = GetStableHash(tag);
        var satSpan = TagColorSaturationMax - TagColorSaturationMin;
        var lightSpan = TagColorLightnessMax - TagColorLightnessMin;
        var saturation = TagColorSaturationMin + (Math.Abs(hash) % 1000) / 1000d * satSpan;
        var lightness = TagColorLightnessMin + (Math.Abs(hash / 1000) % 1000) / 1000d * lightSpan;
        return BuildHexColor(baseHue, saturation, lightness);
    }

    private IReadOnlyDictionary<string, TagCountBreakdown> BuildTagCounts(string libraryRoot, string filePath, IReadOnlySet<string> stopWords)
    {
        var relativePath = Path.GetRelativePath(libraryRoot, filePath);
        var segments = relativePath.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        var fileNameSegment = segments.Length == 0
            ? Path.GetFileNameWithoutExtension(filePath)
            : Path.GetFileNameWithoutExtension(segments[^1]);
        var pathSegments = segments.Length <= 1 ? Array.Empty<string>() : segments[..^1];

        var fileNameCounts = _tokenizer.Tokenize(new[] { fileNameSegment }, stopWords);
        var pathCounts = _tokenizer.Tokenize(pathSegments, stopWords);
        var contentCounts = TokenizeFileContents(filePath, stopWords);

        var counts = new Dictionary<string, TagCountBreakdown>(StringComparer.Ordinal);
        MergeCounts(counts, fileNameCounts, (breakdown, value) => breakdown.FileNameCount += value);
        MergeCounts(counts, pathCounts, (breakdown, value) => breakdown.PathCount += value);
        MergeCounts(counts, contentCounts, (breakdown, value) => breakdown.ContentCount += value);

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

    private static SqliteCommand CreateCommand(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        string param1Name,
        string param2Name,
        string param3Name,
        out SqliteParameter param1,
        out SqliteParameter param2,
        out SqliteParameter param3,
        string param4Name,
        string param5Name,
        string param6Name,
        out SqliteParameter param4,
        out SqliteParameter param5,
        out SqliteParameter param6)
    {
        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        param1 = command.CreateParameter();
        param2 = command.CreateParameter();
        param3 = command.CreateParameter();
        param4 = command.CreateParameter();
        param5 = command.CreateParameter();
        param6 = command.CreateParameter();
        param1.ParameterName = param1Name;
        param2.ParameterName = param2Name;
        param3.ParameterName = param3Name;
        param4.ParameterName = param4Name;
        param5.ParameterName = param5Name;
        param6.ParameterName = param6Name;
        command.Parameters.Add(param1);
        command.Parameters.Add(param2);
        command.Parameters.Add(param3);
        command.Parameters.Add(param4);
        command.Parameters.Add(param5);
        command.Parameters.Add(param6);
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

    private sealed record TagInfo(long Id, string Name);

    private sealed record TagEdge(long TargetId, int Weight);

    private sealed record TagColorState(int LastTagCount, string LastTagHash);

    private sealed record TagColorEntry(string ColorHex, int ClusterId);

    private sealed class TagCountBreakdown
    {
        public int FileNameCount { get; set; }
        public int PathCount { get; set; }
        public int ContentCount { get; set; }
        public int TotalCount => FileNameCount + PathCount + ContentCount;
    }

    private static void MergeCounts(
        IDictionary<string, TagCountBreakdown> target,
        IReadOnlyDictionary<string, int> counts,
        Action<TagCountBreakdown, int> addCount)
    {
        foreach (var entry in counts)
        {
            if (!target.TryGetValue(entry.Key, out var breakdown))
            {
                breakdown = new TagCountBreakdown();
                target[entry.Key] = breakdown;
            }

            addCount(breakdown, entry.Value);
        }
    }
}
