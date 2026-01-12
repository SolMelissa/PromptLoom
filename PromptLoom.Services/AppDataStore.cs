// CHANGE LOG
// - 2026-03-09 | Request: Categories AppData path | Point library storage to AppData/Local/PromptLoom/Categories and migrate legacy Library data.
// - 2026-03-06 | Request: Tag-only mode | Replace category storage with a library root and seed on first run.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PromptLoom.Services;

/// <summary>
/// Centralizes persistent user-data locations and first-run seeding.
/// </summary>
public static class AppDataStore
{
    private const string CategoriesFolderName = "Categories";
    private const string LegacyLibraryFolderName = "Library";

    public static string RootDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PromptLoom");

    public static string CategoriesDir => Path.Combine(RootDir, CategoriesFolderName);
    public static string LibraryDir => CategoriesDir;
    public static string OutputDir => Path.Combine(RootDir, "Output");

    /// <summary>
    /// Ensures the AppData store exists and seeds the library if needed.
    /// </summary>
    public static void EnsureInitialized(string installDir, ErrorReporter errors)
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(OutputDir);

        try
        {
            Directory.CreateDirectory(CategoriesDir);
            if (Directory.EnumerateFileSystemEntries(CategoriesDir).Any())
                return;

            var legacyAppLibrary = Path.Combine(RootDir, LegacyLibraryFolderName);
            if (Directory.Exists(legacyAppLibrary) && Directory.EnumerateFileSystemEntries(legacyAppLibrary).Any())
            {
                CopyDirectory(legacyAppLibrary, CategoriesDir);
                errors.Info("Migrated AppData library into Categories.", "AppDataStore");
                return;
            }

            var installCategories = Path.Combine(installDir, CategoriesFolderName);
            if (Directory.Exists(installCategories))
            {
                CopyDirectory(installCategories, CategoriesDir);
                errors.Info("Seeded categories from install folder.", "AppDataStore");
                return;
            }

            var installLibrary = Path.Combine(installDir, LegacyLibraryFolderName);
            if (Directory.Exists(installLibrary))
            {
                CopyDirectory(installLibrary, CategoriesDir);
                errors.Info("Seeded categories from install library folder.", "AppDataStore");
                return;
            }

            var categoriesZipPath = Path.Combine(installDir, $"{CategoriesFolderName}.zip");
            if (File.Exists(categoriesZipPath))
            {
                ZipFile.ExtractToDirectory(categoriesZipPath, CategoriesDir, overwriteFiles: true);
                errors.Info("Seeded categories from Categories.zip.", "AppDataStore");
                return;
            }

            var legacyZipPath = Path.Combine(installDir, $"{LegacyLibraryFolderName}.zip");
            if (File.Exists(legacyZipPath))
            {
                ZipFile.ExtractToDirectory(legacyZipPath, CategoriesDir, overwriteFiles: true);
                errors.Info("Seeded categories from Library.zip.", "AppDataStore");
            }
        }
        catch (Exception ex)
        {
            errors.Report(ex, "AppDataStore.EnsureInitialized");
        }
    }

    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir))
        {
            var name = Path.GetFileName(file);
            File.Copy(file, Path.Combine(destDir, name), overwrite: true);
        }

        foreach (var dir in Directory.EnumerateDirectories(sourceDir))
        {
            var name = Path.GetFileName(dir);
            CopyDirectory(dir, Path.Combine(destDir, name));
        }
    }
}
