// CHANGE LOG
// - 2026-03-07 | Fix: Lemmatization reindex | Bump the index version so existing data is rebuilt with the new tokenizer.
// - 2026-03-06 | Request: Tag-only mode | Rename index root to library storage.
// - 2026-03-06 | Request: TF-IDF metadata | Store per-tag occurring file counts in the index.
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using SQLitePCL;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for tag index storage and schema initialization.
/// </summary>
public interface ITagIndexStore
{
    /// <summary>
    /// Returns the absolute path to the tag index database.
    /// </summary>
    string DatabasePath { get; }

    /// <summary>
    /// Creates a new SQLite connection for the tag index.
    /// </summary>
    SqliteConnection CreateConnection();

    /// <summary>
    /// Ensures the tag index database and schema exist.
    /// </summary>
    Task InitializeAsync(CancellationToken ct = default);

    /// <summary>
    /// Rebuilds the FTS index from the Tags table.
    /// </summary>
    Task RebuildTagFtsAsync(CancellationToken ct = default);
}

/// <summary>
/// SQLite-backed tag index store for prompt search metadata.
/// </summary>
public sealed class TagIndexStore : ITagIndexStore
{
    private const int SchemaVersion = 5;
    private const int DefaultIndexVersion = 5;
    private readonly IFileSystem _fileSystem;
    private readonly IAppDataStore _appDataStore;

    /// <summary>
    /// Creates a new tag index store.
    /// </summary>
    public TagIndexStore(IFileSystem fileSystem, IAppDataStore appDataStore)
    {
        _fileSystem = fileSystem;
        _appDataStore = appDataStore;
    }

    /// <inheritdoc/>
    public string DatabasePath => Path.Combine(_appDataStore.RootDir, "DBs", "Tags.db");

    /// <inheritdoc/>
    public SqliteConnection CreateConnection()
        => new($"Data Source={DatabasePath}");

    /// <inheritdoc/>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        Batteries_V2.Init();
        _fileSystem.CreateDirectory(Path.GetDirectoryName(DatabasePath) ?? _appDataStore.RootDir);

        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);

        await ExecuteNonQueryAsync(connection, "PRAGMA foreign_keys = ON;", ct);
        await ExecuteNonQueryAsync(connection, "PRAGMA journal_mode = WAL;", ct);

        const string schemaSql = @"
CREATE TABLE IF NOT EXISTS Files (
    Id INTEGER PRIMARY KEY,
    Path TEXT NOT NULL UNIQUE,
    FileName TEXT NOT NULL,
    LastWriteTicks INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS Tags (
    Id INTEGER PRIMARY KEY,
    Name TEXT NOT NULL UNIQUE,
    OccurringFileCount INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS FileTags (
    FileId INTEGER NOT NULL,
    TagId INTEGER NOT NULL,
    OccurrenceCount INTEGER NOT NULL,
    FileNameCount INTEGER NOT NULL DEFAULT 0,
    PathCount INTEGER NOT NULL DEFAULT 0,
    ContentCount INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (FileId, TagId),
    FOREIGN KEY (FileId) REFERENCES Files(Id) ON DELETE CASCADE,
    FOREIGN KEY (TagId) REFERENCES Tags(Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_FileTags_TagId ON FileTags(TagId);
CREATE INDEX IF NOT EXISTS IX_FileTags_FileId ON FileTags(FileId);
CREATE TABLE IF NOT EXISTS IndexState (
    Id INTEGER PRIMARY KEY CHECK (Id = 1),
    SchemaVersion INTEGER NOT NULL,
    LastScanTicks INTEGER NOT NULL,
    LibraryRoot TEXT NOT NULL,
    IndexVersion INTEGER NOT NULL DEFAULT 1
);";

        await ExecuteNonQueryAsync(connection, schemaSql, ct);
        await EnsureIndexVersionColumnAsync(connection, ct);
        await EnsureLibraryRootColumnAsync(connection, ct);
        await EnsureTagColumnsAsync(connection, ct);
        await EnsureFileTagColumnsAsync(connection, ct);

        await ExecuteNonQueryAsync(
            connection,
            "CREATE VIRTUAL TABLE IF NOT EXISTS TagFts USING fts5(Name, content='Tags', content_rowid='Id');",
            ct);

        await using (var stateCommand = connection.CreateCommand())
        {
            stateCommand.CommandText = @"
INSERT OR IGNORE INTO IndexState (Id, SchemaVersion, LastScanTicks, LibraryRoot, IndexVersion)
VALUES (1, $schemaVersion, 0, $libraryRoot, $indexVersion);
UPDATE IndexState SET SchemaVersion = $schemaVersion, LibraryRoot = $libraryRoot WHERE Id = 1;";
            stateCommand.Parameters.AddWithValue("$schemaVersion", SchemaVersion);
            stateCommand.Parameters.AddWithValue("$libraryRoot", _appDataStore.LibraryDir);
            stateCommand.Parameters.AddWithValue("$indexVersion", DefaultIndexVersion);
            await stateCommand.ExecuteNonQueryAsync(ct);
        }
    }

    /// <inheritdoc/>
    public async Task RebuildTagFtsAsync(CancellationToken ct = default)
    {
        Batteries_V2.Init();
        await using var connection = CreateConnection();
        await connection.OpenAsync(ct);
        await ExecuteNonQueryAsync(connection, "INSERT INTO TagFts(TagFts) VALUES('rebuild');", ct);
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task EnsureIndexVersionColumnAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(IndexState);";
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            if (string.Equals(reader.GetString(1), "IndexVersion", StringComparison.OrdinalIgnoreCase))
                return;
        }

        await ExecuteNonQueryAsync(
            connection,
            "ALTER TABLE IndexState ADD COLUMN IndexVersion INTEGER NOT NULL DEFAULT 1;",
            ct);
    }

    private static async Task EnsureLibraryRootColumnAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(IndexState);";
        await using var reader = await command.ExecuteReaderAsync(ct);

        while (await reader.ReadAsync(ct))
        {
            if (string.Equals(reader.GetString(1), "LibraryRoot", StringComparison.OrdinalIgnoreCase))
                return;
        }

        await ExecuteNonQueryAsync(
            connection,
            "ALTER TABLE IndexState ADD COLUMN LibraryRoot TEXT NOT NULL DEFAULT '';",
            ct);
    }

    private static async Task EnsureFileTagColumnsAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(FileTags);";
        await using var reader = await command.ExecuteReaderAsync(ct);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(ct))
            columns.Add(reader.GetString(1));

        if (!columns.Contains("FileNameCount"))
        {
            await ExecuteNonQueryAsync(
                connection,
                "ALTER TABLE FileTags ADD COLUMN FileNameCount INTEGER NOT NULL DEFAULT 0;",
                ct);
        }

        if (!columns.Contains("PathCount"))
        {
            await ExecuteNonQueryAsync(
                connection,
                "ALTER TABLE FileTags ADD COLUMN PathCount INTEGER NOT NULL DEFAULT 0;",
                ct);
        }

        if (!columns.Contains("ContentCount"))
        {
            await ExecuteNonQueryAsync(
                connection,
                "ALTER TABLE FileTags ADD COLUMN ContentCount INTEGER NOT NULL DEFAULT 0;",
                ct);
        }
    }

    private static async Task EnsureTagColumnsAsync(SqliteConnection connection, CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(Tags);";
        await using var reader = await command.ExecuteReaderAsync(ct);

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync(ct))
            columns.Add(reader.GetString(1));

        if (!columns.Contains("OccurringFileCount"))
        {
            await ExecuteNonQueryAsync(
                connection,
                "ALTER TABLE Tags ADD COLUMN OccurringFileCount INTEGER NOT NULL DEFAULT 0;",
                ct);
        }
    }
}
