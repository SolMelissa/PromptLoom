// FIX: Allow prompt generation and previews to use an injectable file reader for unit testing.
// CAUSE: Static file access in core logic prevented deterministic tests without filesystem access.
// CHANGE: Introduce IWildcardFileReader and a default adapter. 2025-12-25

using System.Collections.Generic;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for loading wildcard entry files.
/// </summary>
public interface IWildcardFileReader
{
    /// <summary>
    /// Loads a wildcard .txt file and returns its non-empty entries.
    /// </summary>
    List<string> LoadWildcardFile(string path);
}

/// <summary>
/// Default wildcard file reader that delegates to the existing static implementation.
/// </summary>
public sealed class WildcardFileReader : IWildcardFileReader
{
    /// <summary>
    /// Loads a wildcard .txt file from disk.
    /// </summary>
    public List<string> LoadWildcardFile(string path) => SchemaFileReader.LoadWildcardFile(path);
}
