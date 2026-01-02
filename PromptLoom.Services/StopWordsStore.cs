// CHANGE LOG
// - 2026-01-02 | Request: Tag search indexing | Add stop-words JSON store under AppData.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for loading and persisting stop words for tag tokenization.
/// </summary>
public interface IStopWordsStore
{
    /// <summary>
    /// Loads the stop-word list, creating a default file if missing.
    /// </summary>
    IReadOnlySet<string> LoadOrCreate();

    /// <summary>
    /// Returns the expected stop-words JSON path.
    /// </summary>
    string StopWordsPath { get; }
}

/// <summary>
/// Default stop-words store using AppData/Config/stop-words.json.
/// </summary>
public sealed class StopWordsStore : IStopWordsStore
{
    private static readonly string[] DefaultStopWords =
    {
        "a",
        "an",
        "and",
        "the",
        "of",
        "to",
        "in",
        "for",
        "on",
        "with"
    };

    private readonly IFileSystem _fileSystem;
    private readonly IAppDataStore _appDataStore;

    /// <summary>
    /// Creates a new stop-words store.
    /// </summary>
    public StopWordsStore(IFileSystem fileSystem, IAppDataStore appDataStore)
    {
        _fileSystem = fileSystem;
        _appDataStore = appDataStore;
    }

    /// <inheritdoc/>
    public string StopWordsPath => Path.Combine(_appDataStore.RootDir, "Config", "stop-words.json");

    /// <inheritdoc/>
    public IReadOnlySet<string> LoadOrCreate()
    {
        var configDir = Path.GetDirectoryName(StopWordsPath) ?? _appDataStore.RootDir;
        _fileSystem.CreateDirectory(configDir);

        if (!_fileSystem.FileExists(StopWordsPath))
        {
            var payload = new StopWordsPayload
            {
                Version = 1,
                StopWords = DefaultStopWords.ToList()
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            _fileSystem.WriteAllText(StopWordsPath, json);
        }

        var contents = _fileSystem.ReadAllText(StopWordsPath);
        var payloadResult = JsonSerializer.Deserialize<StopWordsPayload>(contents);
        if (payloadResult == null)
            throw new InvalidOperationException("stop-words.json could not be parsed.");

        var words = payloadResult.StopWords ?? new List<string>();
        return new HashSet<string>(
            words.Select(word => word.Trim().ToLowerInvariant()).Where(word => word.Length > 0),
            StringComparer.Ordinal);
    }

    private sealed class StopWordsPayload
    {
        public int Version { get; set; } = 1;
        public List<string>? StopWords { get; set; }
    }
}
