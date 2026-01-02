// FIX: Define the missing file system abstraction used by UserSettingsStore to restore build.
// CAUSE: IFileSystem interface was referenced but not defined in the main project sources.
// CHANGE: Add IFileSystem and a System.IO-backed implementation. 2025-12-26

using System.Collections.Generic;
using System.IO;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for file system operations used by the app.
/// </summary>
public interface IFileSystem
{
    /// <summary>
    /// Returns true if the file exists.
    /// </summary>
    bool FileExists(string path);

    /// <summary>
    /// Returns true if the directory exists.
    /// </summary>
    bool DirectoryExists(string path);

    /// <summary>
    /// Creates the directory if it does not exist.
    /// </summary>
    void CreateDirectory(string path);

    /// <summary>
    /// Reads all text from a file.
    /// </summary>
    string ReadAllText(string path);

    /// <summary>
    /// Reads all lines from a file.
    /// </summary>
    string[] ReadAllLines(string path);

    /// <summary>
    /// Writes all text to a file, creating it if needed.
    /// </summary>
    void WriteAllText(string path, string contents);

    /// <summary>
    /// Appends text to a file, creating it if needed.
    /// </summary>
    void AppendAllText(string path, string contents);

    /// <summary>
    /// Deletes a file if it exists.
    /// </summary>
    void DeleteFile(string path);

    /// <summary>
    /// Deletes a directory.
    /// </summary>
    void DeleteDirectory(string path, bool recursive);

    /// <summary>
    /// Copies a file.
    /// </summary>
    void CopyFile(string sourceFileName, string destFileName, bool overwrite);

    /// <summary>
    /// Returns subdirectories within the specified directory.
    /// </summary>
    IEnumerable<string> GetDirectories(string path);

    /// <summary>
    /// Enumerates subdirectories within the specified directory.
    /// </summary>
    IEnumerable<string> EnumerateDirectories(string path);

    /// <summary>
    /// Returns files matching the search pattern.
    /// </summary>
    IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption);

    /// <summary>
    /// Enumerates both files and directories in the specified directory.
    /// </summary>
    IEnumerable<string> EnumerateFileSystemEntries(string path);

    /// <summary>
    /// Enumerates files matching the search pattern.
    /// </summary>
    IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption);

    /// <summary>
    /// Returns the parent directory path, or null if none.
    /// </summary>
    string? GetParentDirectory(string path);
}

/// <summary>
/// Default file system implementation using System.IO.
/// </summary>
public sealed class FileSystem : IFileSystem
{
    /// <inheritdoc/>
    public bool FileExists(string path) => File.Exists(path);

    /// <inheritdoc/>
    public bool DirectoryExists(string path) => Directory.Exists(path);

    /// <inheritdoc/>
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);

    /// <inheritdoc/>
    public string ReadAllText(string path) => File.ReadAllText(path);

    /// <inheritdoc/>
    public string[] ReadAllLines(string path) => File.ReadAllLines(path);

    /// <inheritdoc/>
    public void WriteAllText(string path, string contents) => File.WriteAllText(path, contents);

    /// <inheritdoc/>
    public void AppendAllText(string path, string contents) => File.AppendAllText(path, contents);

    /// <inheritdoc/>
    public void DeleteFile(string path)
    {
        if (File.Exists(path))
            File.Delete(path);
    }

    /// <inheritdoc/>
    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);

    /// <inheritdoc/>
    public void CopyFile(string sourceFileName, string destFileName, bool overwrite)
        => File.Copy(sourceFileName, destFileName, overwrite);

    /// <inheritdoc/>
    public IEnumerable<string> GetDirectories(string path) => Directory.GetDirectories(path);

    /// <inheritdoc/>
    public IEnumerable<string> EnumerateDirectories(string path) => Directory.EnumerateDirectories(path);

    /// <inheritdoc/>
    public IEnumerable<string> GetFiles(string path, string searchPattern, SearchOption searchOption)
        => Directory.GetFiles(path, searchPattern, searchOption);

    /// <inheritdoc/>
    public IEnumerable<string> EnumerateFileSystemEntries(string path)
        => Directory.EnumerateFileSystemEntries(path);

    /// <inheritdoc/>
    public IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        => Directory.EnumerateFiles(path, searchPattern, searchOption);

    /// <inheritdoc/>
    public string? GetParentDirectory(string path) => Directory.GetParent(path)?.FullName;
}
