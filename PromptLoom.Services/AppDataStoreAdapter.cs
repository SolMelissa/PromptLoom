// CHANGE LOG
// - 2026-03-09 | Request: Categories AppData path | Document LibraryDir as Categories storage for legacy naming.
// - 2026-03-06 | Request: Tag-only mode | Rename app data contract to library storage.
// - 2025-12-25 | Fix: AppDataStore seam | Wrap static AppDataStore access to allow injection in tests.

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
    /// Categories directory (legacy name: LibraryDir).
    /// </summary>
    string LibraryDir { get; }

    /// <summary>
    /// Output directory.
    /// </summary>
    string OutputDir { get; }

    /// <summary>
    /// Ensures the AppData store is initialized.
    /// </summary>
    void EnsureInitialized(string installDir, IErrorReporter errors);
}

/// <summary>
/// Default adapter that delegates to AppDataStore.
/// </summary>
public sealed class AppDataStoreAdapter : IAppDataStore
{
    /// <inheritdoc/>
    public string RootDir => AppDataStore.RootDir;
    /// <inheritdoc/>
    public string LibraryDir => AppDataStore.LibraryDir;
    /// <inheritdoc/>
    public string OutputDir => AppDataStore.OutputDir;

    /// <inheritdoc/>
    public void EnsureInitialized(string installDir, IErrorReporter errors)
        => AppDataStore.EnsureInitialized(installDir, ResolveReporter(errors));

    private static ErrorReporter ResolveReporter(IErrorReporter errors)
        => (errors as ErrorReporterAdapter)?.Inner ?? ErrorReporter.Instance;
}
