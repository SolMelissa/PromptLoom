// CHANGE LOG
// - 2026-03-09 | Fix: Prompt normalization | Collapse internal whitespace when normalizing tag entries.
// - 2026-03-06 | Request: Tag-only generation | Build prompts directly from selected tag files.

using System;
using System.Collections.Generic;
using System.Linq;

namespace PromptLoom.Services;

/// <summary>
/// Builds prompt text from a list of selected files.
/// </summary>
public sealed class TagPromptBuilder
{
    private readonly IWildcardFileReader _fileReader;
    private readonly IRandomSource _randomSource;

    public sealed record TagPromptResult(string Prompt, List<string> Messages);

    public TagPromptBuilder(IWildcardFileReader? fileReader = null, IRandomSource? randomSource = null)
    {
        _fileReader = fileReader ?? new WildcardFileReader();
        _randomSource = randomSource ?? new SystemRandomSource();
    }

    public TagPromptResult Generate(IReadOnlyList<string> filePaths, int? seed)
    {
        var messages = new List<string>();
        if (filePaths.Count == 0)
        {
            messages.Add("Select files to build a prompt.");
            return new TagPromptResult(string.Empty, messages);
        }

        var rng = _randomSource.Create(seed);
        var parts = new List<string>();

        foreach (var path in filePaths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            List<string> entries;
            try
            {
                entries = _fileReader.LoadWildcardFile(path);
            }
            catch (Exception ex)
            {
                messages.Add($"Could not read file '{path}': {ex.Message}");
                continue;
            }

            if (entries.Count == 0)
            {
                messages.Add($"File '{path}' has no entries.");
                continue;
            }

            var pick = entries[rng.Next(entries.Count)];
            if (!string.IsNullOrWhiteSpace(pick))
                parts.Add(Normalize(pick));
        }

        var prompt = string.Join("\n", parts);
        if (string.IsNullOrWhiteSpace(prompt))
            messages.Add("Prompt is empty. Select files with usable entries.");

        return new TagPromptResult(prompt, messages);
    }

    private static string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var cleaned = value.Replace("\r", " ").Replace("\n", " ").Trim();
        return string.Join(" ", cleaned.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }
}
