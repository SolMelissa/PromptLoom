// FIX: Wrap static AppDataStore access to allow injection in tests.
// CAUSE: Static AppDataStore usage in view models made AppData paths hard to mock.
// CHANGE: Introduce IAppDataStore and a default adapter. 2025-12-25
// FIX: Accept IErrorReporter for logging seam support.
// CAUSE: AppDataStore methods require ErrorReporter; tests use IErrorReporter.
// CHANGE: Adapt IErrorReporter to ErrorReporter.Instance when needed. 2025-12-25

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for AppData paths and initialization.
/// </summary>
public interface IAppDataStore
{
    /// <summary>
    /// Root directory for app data.
    /// </summary>
    string RootDir { get; }

    /// <summary>
    /// Categories directory.
    /// </summary>
    string CategoriesDir { get; }

    /// <summary>
    /// Output directory.
    /// </summary>
    string OutputDir { get; }

    /// <summary>
    /// Ensures the AppData store is initialized.
    /// </summary>
    void EnsureInitialized(string installDir, IErrorReporter errors);

    /// <summary>
    /// Restores bundled categories.
    /// </summary>
    void RestoreBundledCategories(string installDir, IErrorReporter errors);
}

/// <summary>
/// Default adapter that delegates to AppDataStore.
/// </summary>
public sealed class AppDataStoreAdapter : IAppDataStore
{
    /// <inheritdoc/>
    public string RootDir => AppDataStore.RootDir;
    /// <inheritdoc/>
    public string CategoriesDir => AppDataStore.CategoriesDir;
    /// <inheritdoc/>
    public string OutputDir => AppDataStore.OutputDir;

    /// <inheritdoc/>
    public void EnsureInitialized(string installDir, IErrorReporter errors)
        => AppDataStore.EnsureInitialized(installDir, ResolveReporter(errors));

    /// <inheritdoc/>
    public void RestoreBundledCategories(string installDir, IErrorReporter errors)
        => AppDataStore.RestoreBundledCategories(installDir, ResolveReporter(errors));

    private static ErrorReporter ResolveReporter(IErrorReporter errors)
        => (errors as ErrorReporterAdapter)?.Inner ?? ErrorReporter.Instance;
}
