using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PromptLoom.Services;

public static class SchemaFileReader
{
    /// <summary>
    /// Loads a wildcard .txt file.
    ///
    /// This method is intentionally defensive: it should never throw.
    /// If a file cannot be read (locked, invalid path, permissions), it returns an empty list.
    /// </summary>
    public static List<string> LoadWildcardFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return new();

            return File.ReadAllLines(path)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l))
                .Where(l => !l.StartsWith("#"))
                .ToList();
        }
        catch
        {
            return new();
        }
    }
}
