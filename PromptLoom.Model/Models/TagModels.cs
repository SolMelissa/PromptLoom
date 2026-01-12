// CHANGE LOG
// - 2026-03-06 | Request: TF-IDF models | Add tag metadata and per-file tag occurrence structures.
using System;
using System.Collections.Generic;

namespace PromptLoom.Models;

public sealed class TagData
{
    public string Name { get; set; } = string.Empty;
    public int OccurringFileCount { get; set; }
}

public sealed class TagOccurrence
{
    public int FileNameCount { get; set; }
    public int PathCount { get; set; }
    public int ContentCount { get; set; }
}

public sealed class FileTagData
{
    public Dictionary<string, TagOccurrence> Tags { get; } = new(StringComparer.OrdinalIgnoreCase);
}
