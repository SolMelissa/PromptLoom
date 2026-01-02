// CHANGE LOG
// - 2026-01-02 | Request: Tag search indexing | Add tag tokenizer with per-token counts.
using System;
using System.Collections.Generic;

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

    private static void AddTokens(IDictionary<string, int> counts, string segment, IReadOnlySet<string> stopWords)
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

                if (counts.TryGetValue(token, out var count))
                    counts[token] = count + 1;
                else
                    counts[token] = 1;
            }
        }
    }
}
