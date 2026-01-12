// CHANGE LOG
// - 2026-03-06 | Request: Expanded stop words | Replace defaults with comprehensive list.
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
        "b",
        "c",
        "d",
        "e",
        "f",
        "g",
        "h",
        "i",
        "j",
        "k",
        "l",
        "m",
        "n",
        "o",
        "p",
        "q",
        "r",
        "s",
        "t",
        "u",
        "v",
        "w",
        "x",
        "y",
        "z",
        "an",
        "the",
        "me",
        "my",
        "mine",
        "myself",
        "we",
        "us",
        "our",
        "ours",
        "ourselves",
        "you",
        "your",
        "yours",
        "yourself",
        "yourselves",
        "he",
        "him",
        "his",
        "himself",
        "she",
        "her",
        "hers",
        "herself",
        "it",
        "its",
        "itself",
        "they",
        "them",
        "their",
        "theirs",
        "themselves",
        "what",
        "which",
        "who",
        "whom",
        "whose",
        "this",
        "that",
        "these",
        "those",
        "about",
        "above",
        "across",
        "after",
        "against",
        "along",
        "amid",
        "among",
        "around",
        "as",
        "at",
        "before",
        "behind",
        "below",
        "beneath",
        "beside",
        "between",
        "beyond",
        "but",
        "by",
        "concerning",
        "down",
        "during",
        "except",
        "for",
        "from",
        "in",
        "inside",
        "into",
        "like",
        "near",
        "of",
        "off",
        "on",
        "onto",
        "out",
        "outside",
        "over",
        "past",
        "regarding",
        "round",
        "since",
        "through",
        "throughout",
        "to",
        "toward",
        "towards",
        "under",
        "underneath",
        "until",
        "up",
        "upon",
        "with",
        "within",
        "without",
        "and",
        "or",
        "nor",
        "yet",
        "so",
        "although",
        "because",
        "unless",
        "while",
        "whereas",
        "whether",
        "am",
        "is",
        "are",
        "was",
        "were",
        "be",
        "been",
        "being",
        "have",
        "has",
        "had",
        "having",
        "do",
        "does",
        "did",
        "doing",
        "will",
        "would",
        "shall",
        "should",
        "can",
        "could",
        "may",
        "might",
        "must",
        "ought",
        "very",
        "too",
        "just",
        "also",
        "now",
        "then",
        "here",
        "there",
        "when",
        "where",
        "why",
        "how",
        "not",
        "no",
        "yes",
        "all",
        "any",
        "both",
        "each",
        "few",
        "more",
        "most",
        "other",
        "some",
        "such",
        "only",
        "own",
        "same",
        "than"
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
