// TEST: In-memory file system for persistence-focused unit tests.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using PromptLoom.Services;

namespace PromptLoom.Tests.Fakes;

/// <summary>
/// Simple in-memory file system implementation for tests.
/// </summary>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);

    public FakeFileSystem AddFile(string path, string contents)
    {
        _files[path] = contents;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(dir))
            _directories.Add(dir);
        return this;
    }

    public bool FileExists(string path) => _files.ContainsKey(path);

    public bool DirectoryExists(string path) => _directories.Contains(path);

    public void CreateDirectory(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
            _directories.Add(path);
    }

    public string ReadAllText(string path) => _files[path];

    public void WriteAllText(string path, string contents)
    {
        AddFile(path, contents);
    }

    public IEnumerable<string> GetDirectories(string path)
        => _directories.Where(d => d.StartsWith(path, StringComparison.OrdinalIgnoreCase)).ToList();

    public IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption)
        => _files.Keys.Where(f => f.StartsWith(path, StringComparison.OrdinalIgnoreCase)).ToList();

    public IEnumerable<string> EnumerateFileSystemEntries(string path)
        => GetDirectories(path).Concat(GetFiles(path, "*", SearchOption.TopDirectoryOnly)).ToList();

    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        => GetFiles(path, searchPattern, searchOption);
}
