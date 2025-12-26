// TEST: Fake AppData store for view model persistence tests.

using PromptLoom.Services;

namespace PromptLoom.Tests.Fakes;

/// <summary>
/// In-memory AppData store for tests.
/// </summary>
public sealed class FakeAppDataStore : IAppDataStore
{
    public string RootDir { get; }
    public string CategoriesDir => RootDir + "\\Categories";
    public string OutputDir => RootDir + "\\Output";

    public FakeAppDataStore(string rootDir)
    {
        RootDir = rootDir;
    }

    public void EnsureInitialized(string installDir, IErrorReporter errors)
    {
        // no-op for tests
    }

    public void RestoreBundledCategories(string installDir, IErrorReporter errors)
    {
        // no-op for tests
    }
}
