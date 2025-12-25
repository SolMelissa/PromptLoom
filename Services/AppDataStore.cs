// Build note (2025-12-22):
// - Error CS1061: 'ErrorReporter' does not contain a definition for 'Error'.
// - Fix: Added ErrorReporter.Error(string, context) helper and kept this call for clarity.

using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace PromptLoom.Services;

/// <summary>
/// Centralizes persistent user-data locations and first-run migration.
/// 
/// Goal: Categories + any user-generated files persist across app versions.
/// </summary>
public static class AppDataStore
{
    public static string RootDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "PromptLoom");

    public static string CategoriesDir => Path.Combine(RootDir, "Categories");
    public static string OutputDir => Path.Combine(RootDir, "Output");

    /// <summary>
    /// Ensures the AppData store exists and contains Categories.
    /// NOTE: As of v1.6.3 we do NOT auto-populate Categories on first run.
    /// Users can explicitly restore the bundled Categories via the menu.
    /// </summary>
    public static void EnsureInitialized(string installDir, ErrorReporter errors)
    {
        Directory.CreateDirectory(RootDir);
        Directory.CreateDirectory(OutputDir);

        // We intentionally do not copy any bundled Categories here.
        // Create the Categories directory if it doesn't exist and stop.
        try
        {
            Directory.CreateDirectory(CategoriesDir);
        }
        catch (Exception ex)
        {
            errors.Report(ex, "AppDataStore.EnsureInitialized");
        }
    }

    /// <summary>
    /// Restores the app's bundled Categories into the AppData CategoriesDir.
    /// Creates a timestamped backup zip of the current CategoriesDir (if any) before overwriting.
    /// </summary>
    public static void RestoreBundledCategories(string installDir, ErrorReporter errors)
    {
        try
        {
            Directory.CreateDirectory(RootDir);
            Directory.CreateDirectory(OutputDir);

            // Backup existing user Categories (if any)
            if (Directory.Exists(CategoriesDir) && Directory.EnumerateFileSystemEntries(CategoriesDir).Any())
            {
                var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var backupZip = Path.Combine(OutputDir, $"CategoriesBackup_{stamp}.zip");
                if (File.Exists(backupZip))
                    File.Delete(backupZip);

                ZipFile.CreateFromDirectory(CategoriesDir, backupZip, CompressionLevel.Optimal, includeBaseDirectory: false);
                errors.Info($"Backed up Categories to {backupZip}", "AppDataStore");
            }

            // Wipe and restore
            if (Directory.Exists(CategoriesDir))
                Directory.Delete(CategoriesDir, recursive: true);

            Directory.CreateDirectory(CategoriesDir);

            var installCategories = Path.Combine(installDir, "Categories");
            if (Directory.Exists(installCategories))
            {
                CopyDirectory(installCategories, CategoriesDir);
                errors.Info("Restored bundled Categories from install folder.", "AppDataStore");
                return;
            }

            var zipPath = Path.Combine(installDir, "Categories.zip");
            if (File.Exists(zipPath))
            {
                ZipFile.ExtractToDirectory(zipPath, CategoriesDir, overwriteFiles: true);
                errors.Info("Restored bundled Categories from Categories.zip.", "AppDataStore");
                return;
            }

            errors.Error("Bundled Categories not found in install folder (no Categories/ or Categories.zip).", "AppDataStore");
        }
        catch (Exception ex)
        {
            errors.Report(ex, "AppDataStore.RestoreBundledCategories");
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
