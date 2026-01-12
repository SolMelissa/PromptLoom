// CHANGE LOG
// - 2026-03-06 | Request: Lemmatization | Normalize tokens with XLemmatizer before counting.
// - 2026-01-02 | Request: Tag search indexing | Add tag tokenizer with per-token counts.
using System;
using System.Collections.Generic;
using System.Linq;
using XLemmatizer;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for tag tokenization rules.
/// </summary>
public interface ITagTokenizer
{
    /// <summary>
    /// Tokenizes the provided segments and returns a count per token.
    /// </summary>
    IReadOnlyDictionary<string, int> Tokenize(IEnumerable<string> segments, IReadOnlySet<string> stopWords);
}

/// <summary>
/// Default tokenizer that splits on non-alphanumeric characters.
/// </summary>
public sealed class TagTokenizer : ITagTokenizer
{
    private readonly Lemmatizer _lemmatizer;
    private static readonly object s_logLock = new();
    private static bool s_hasLoggedLemmatizerFailure;

    /// <summary>
    /// Creates a tokenizer that normalizes tokens with the provided lemmatizer.
    /// </summary>
    public TagTokenizer(Lemmatizer lemmatizer)
    {
        _lemmatizer = lemmatizer ?? throw new ArgumentNullException(nameof(lemmatizer));
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, int> Tokenize(IEnumerable<string> segments, IReadOnlySet<string> stopWords)
    {
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var segment in segments)
        {
            AddTokens(counts, segment, stopWords);
        }

        return counts;
    }

    private void AddTokens(IDictionary<string, int> counts, string segment, IReadOnlySet<string> stopWords)
    {
        if (string.IsNullOrWhiteSpace(segment))
            return;

        var tokenStart = -1;
        for (var i = 0; i <= segment.Length; i++)
        {
            var isTokenChar = i < segment.Length && char.IsLetterOrDigit(segment[i]);
            if (isTokenChar)
            {
                if (tokenStart < 0)
                    tokenStart = i;
            }
            else if (tokenStart >= 0)
            {
                var token = segment.Substring(tokenStart, i - tokenStart).ToLowerInvariant();
                tokenStart = -1;

                if (stopWords.Contains(token))
                    continue;

                var normalized = LemmatizeToken(token);
                if (counts.TryGetValue(normalized, out var count))
                    counts[normalized] = count + 1;
                else
                    counts[normalized] = 1;
            }
        }
    }

    private string LemmatizeToken(string token)
    {
        try
        {
            var tokens = _lemmatizer.Lemmatize(token);
            var lemma = tokens.FirstOrDefault()?.Lemma;
            return string.IsNullOrWhiteSpace(lemma) ? token : lemma.ToLowerInvariant();
        }
        catch (Exception ex)
        {
            LogLemmatizerFailure(ex, token);
            return token;
        }
    }

    private static void LogLemmatizerFailure(Exception ex, string token)
    {
        lock (s_logLock)
        {
            if (s_hasLoggedLemmatizerFailure)
                return;

            ErrorReporter.Instance.Error($"Lemmatizer failed for '{token}': {ex.Message}", "TagTokenizer");
            s_hasLoggedLemmatizerFailure = true;
        }
    }
}
