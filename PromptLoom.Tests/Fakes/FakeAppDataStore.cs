// CHANGE LOG
// - 2026-03-09 | Request: Categories AppData path | Point fake app data storage at Categories folder.
// - 2026-03-06 | Request: Tag-only mode | Update fake app data store to library-based layout.
// TEST: Fake AppData store for view model persistence tests.

using PromptLoom.Services;

namespace PromptLoom.Tests.Fakes;

/// <summary>
/// In-memory AppData store for tests.
/// </summary>
public sealed class FakeAppDataStore : IAppDataStore
{
    public string RootDir { get; }
    public string LibraryDir => RootDir + "\\Categories";
    public string OutputDir => RootDir + "\\Output";

    public FakeAppDataStore(string rootDir)
    {
        RootDir = rootDir;
    }

    public void EnsureInitialized(string installDir, IErrorReporter errors)
    {
        // no-op for tests
    }
}
