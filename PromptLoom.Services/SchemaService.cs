// NOTE (PromptLoom 1.7.0):
// Loader/saver now treats each *.txt as a PromptEntryModel under SubCategoryModel.Entries.
// Includes migration from legacy SubCategoryMeta (UseAllTxtFiles + SelectedTxtFile) to new Entries[] metadata.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using PromptLoom.Models;

namespace PromptLoom.Services;

/// <summary>
/// Abstraction for schema load/save and base folder access.
/// </summary>
public interface ISchemaService
{
    /// <summary>
    /// Root directory for schema content.
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
    /// Ensures base folders exist.
    /// </summary>
    void EnsureBaseFolders();

    /// <summary>
    /// Loads categories from disk.
    /// </summary>
    List<CategoryModel> Load();

    /// <summary>
    /// Saves categories to disk.
    /// </summary>
    void Save(IEnumerable<CategoryModel> categories);
}

public sealed class SchemaService : ISchemaService
{
    public static readonly string[] DefaultCategories =
        ["Subject", "Setting", "Clothing", "Style", "Camera", "Lighting", "Composition", "Quality"];

    public string RootDir { get; }
    public string CategoriesDir => Path.Combine(RootDir, "Categories");
    public string OutputDir => Path.Combine(RootDir, "Output");

    // IMPORTANT:
    // Category/Subcategory json in the wild is commonly camelCase (prefix/suffix) while
    // our C# models are PascalCase (Prefix/Suffix). System.Text.Json is case-sensitive by default,
    // so without these options prefixes/suffixes would deserialize as empty strings.
    private readonly JsonSerializerOptions _json = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public SchemaService(string rootDir) => RootDir = rootDir;

    public void EnsureBaseFolders()
    {
        Directory.CreateDirectory(CategoriesDir);
        Directory.CreateDirectory(OutputDir);
        // As of v1.6.3, PromptLoom does not auto-seed Categories.
        // Users can restore the bundled starter Categories explicitly via the menu.
    }

    
    private static void ApplyEntryMeta(SubCategoryModel sub, SubCategoryMeta meta, List<string> txtPaths)
    {
        // Clear then repopulate Entries based on filesystem, applying meta if present.
        sub.Entries.Clear();

        var fileNames = txtPaths
            .Select(p => (Path: p, Name: Path.GetFileName(p) ?? p))
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .ToList();

        // Build lookup from meta entries (if any)
        var metaMap = (meta.Entries ?? new())
            .Where(e => !string.IsNullOrWhiteSpace(e.Name))
            .GroupBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Legacy defaults:
        // - If meta has no Entries, treat all files as enabled.
        //   We do NOT auto-disable other files in single-file mode; that caused "force checked" behavior.
        var legacySelected = meta.SelectedTxtFile;

        foreach (var f in fileNames)
        {
            var entry = new PromptEntryModel(f.Name, f.Path);

            if (metaMap.TryGetValue(f.Name, out var em))
            {
                entry.Enabled = em.Enabled;
                entry.Order = em.Order;
            }
            else
            {
                entry.Enabled = true;
                entry.Order = 0;
            }

            sub.Entries.Add(entry);
        }

        // Stable ordering: Order then name.
        var ordered = sub.Entries
            .Select((e, idx) => (e, idx))
            .OrderBy(t => t.e.Order)
            .ThenBy(t => t.e.Name)
            .ThenBy(t => t.idx)
            .Select(t => t.e)
            .ToList();
        sub.Entries.Clear();
        foreach (var e in ordered) sub.Entries.Add(e);

        // Normalize order to index for stability.
        for (var i = 0; i < sub.Entries.Count; i++)
            sub.Entries[i].Order = i;

        // Ensure SelectedEntry exists.
        if (!string.IsNullOrWhiteSpace(meta.SelectedTxtFile))
        {
            sub.SelectedTxtFile = meta.SelectedTxtFile;
        }
        if (sub.SelectedEntry is null)
        {
            // Prefer first enabled entry if any.
            sub.SelectedEntry = sub.Entries.FirstOrDefault(e => e.Enabled) ?? sub.Entries.FirstOrDefault();
        }

        // Do not force-enable any file here.
        // The user may intentionally disable all files temporarily.
        // Prompt generation handles edge cases without mutating checkbox state.
    }

public List<CategoryModel> Load()
    {
        EnsureBaseFolders();

        const string RootSubName = "/";
        const string RootSubMetaFileName = "_root_subcategory.json";

        static bool IsLegacyNegative(string? name)
            => string.Equals(name, "Negative", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "Negative Prompt", StringComparison.OrdinalIgnoreCase)
               || string.Equals(name, "Negative Prompts", StringComparison.OrdinalIgnoreCase);

        var catFolders = Directory.GetDirectories(CategoriesDir)
            .Select(d => Path.GetFileName(d) ?? string.Empty)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            // Negative prompts were removed from PromptLoom. Hide any legacy folder to avoid confusion.
            .Where(n => !IsLegacyNegative(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var categories = new List<CategoryModel>();

        foreach (var catName in catFolders)
        {
            if (IsLegacyNegative(catName))
                continue;

            var catPath = Path.Combine(CategoriesDir, catName);
            Directory.CreateDirectory(catPath);

            var catMetaPath = Path.Combine(catPath, "_category.json");
            if (!File.Exists(catMetaPath))
                File.WriteAllText(catMetaPath, JsonSerializer.Serialize(new CategoryMeta(), _json));

            var catMeta = JsonSerializer.Deserialize<CategoryMeta>(File.ReadAllText(catMetaPath), _json) ?? new CategoryMeta();

            var catModel = new CategoryModel(catName, catPath)
            {
                Prefix = catMeta.Prefix,
                Suffix = catMeta.Suffix,
                Enabled = catMeta.Enabled,
                IsLocked = catMeta.IsLocked,
                Order = catMeta.Order
            };

            // Root ("/") subcategory: captures any .txt files directly inside the category folder.
            // This lets users keep a category simple without creating subfolders for every file.
            var rootTxts = Directory.GetFiles(catPath, "*.txt", SearchOption.TopDirectoryOnly)
                .OrderBy(Path.GetFileName)
                .ToList();
            if (rootTxts.Count > 0)
            {
                var rootMetaPath = Path.Combine(catPath, RootSubMetaFileName);
                if (!File.Exists(rootMetaPath))
                    File.WriteAllText(rootMetaPath, JsonSerializer.Serialize(new SubCategoryMeta(), _json));

                var rootMeta = JsonSerializer.Deserialize<SubCategoryMeta>(File.ReadAllText(rootMetaPath), _json) ?? new SubCategoryMeta();
                var rootModel = new SubCategoryModel(RootSubName, catPath)
                {
                    Prefix = rootMeta.Prefix,
                    Suffix = rootMeta.Suffix,
                    Enabled = rootMeta.Enabled,
                    IsLocked = rootMeta.IsLocked,
                    UseAllTxtFiles = rootMeta.UseAllTxtFiles,
                    SelectedTxtFile = rootMeta.SelectedTxtFile,
                    Order = -1
                };

                ApplyEntryMeta(rootModel, rootMeta, rootTxts);
                catModel.SubCategories.Add(rootModel);
            }

            foreach (var subDir in Directory.GetDirectories(catPath))
            {
                var subName = Path.GetFileName(subDir) ?? "";
                if (subName.Length == 0) continue;

                var subMetaPath = Path.Combine(subDir, "_subcategory.json");
                if (!File.Exists(subMetaPath))
                    File.WriteAllText(subMetaPath, JsonSerializer.Serialize(new SubCategoryMeta(), _json));

                var subMeta = JsonSerializer.Deserialize<SubCategoryMeta>(File.ReadAllText(subMetaPath), _json) ?? new SubCategoryMeta();

                var subModel = new SubCategoryModel(subName, subDir)
                {
                    Prefix = subMeta.Prefix,
                    Suffix = subMeta.Suffix,
                    Enabled = subMeta.Enabled,
                    IsLocked = subMeta.IsLocked,
                    UseAllTxtFiles = subMeta.UseAllTxtFiles,
                    SelectedTxtFile = subMeta.SelectedTxtFile,
                    Order = subMeta.Order
                };

                var txts = Directory.GetFiles(subDir, "*.txt").OrderBy(Path.GetFileName).ToList();
                if (txts.Count == 0)
                {
                    var placeholder = Path.Combine(subDir, $"{subName}.txt");
                    File.WriteAllText(placeholder, "");
                    txts.Add(placeholder);
                }

                ApplyEntryMeta(subModel, subMeta, txts);
                catModel.SubCategories.Add(subModel);
            }

            // Preserve explicit ordering if present; fall back to alphabetical.
            // If multiple subs have default order 0, keep them stable by then sorting by name.
            var orderedSubs = catModel.SubCategories
                .Select((s, idx) => (s, idx))
                .OrderBy(t => t.s.Order)
                .ThenBy(t => t.s.Name)
                .Select(t => t.s)
                .ToList();
            catModel.SubCategories.Clear();
            foreach (var s in orderedSubs) catModel.SubCategories.Add(s);

            // Normalize sub Orders to their current index for stability.
            for (var i = 0; i < catModel.SubCategories.Count; i++)
                catModel.SubCategories[i].Order = i;

            categories.Add(catModel);
        }

        return categories.OrderBy(c => c.Order).ThenBy(c => c.Name).ToList();
    }

    public void Save(IEnumerable<CategoryModel> categories)
    {
        EnsureBaseFolders();

        const string RootSubName = "/";
        const string RootSubMetaFileName = "_root_subcategory.json";

        foreach (var cat in categories)
        {
            Directory.CreateDirectory(cat.FolderPath);

            var catMetaPath = Path.Combine(cat.FolderPath, "_category.json");
            var cm = new CategoryMeta
            {
                Prefix = cat.Prefix ?? "",
                Suffix = cat.Suffix ?? "",
                Enabled = cat.Enabled,
                IsLocked = cat.IsLocked,
                Order = cat.Order
            };
            File.WriteAllText(catMetaPath, JsonSerializer.Serialize(cm, _json));

            foreach (var sub in cat.SubCategories)
            {
                // Root ("/") subcategory stores its metadata on the category folder itself.
                var isRoot = sub.Name == RootSubName && string.Equals(sub.FolderPath, cat.FolderPath, StringComparison.OrdinalIgnoreCase);

                if (!isRoot)
                    Directory.CreateDirectory(sub.FolderPath);

                var subMetaPath = isRoot
                    ? Path.Combine(cat.FolderPath, RootSubMetaFileName)
                    : Path.Combine(sub.FolderPath, "_subcategory.json");
                var sm = new SubCategoryMeta
                {
                    Prefix = sub.Prefix ?? "",
                    Suffix = sub.Suffix ?? "",
                    Enabled = sub.Enabled,
                    IsLocked = sub.IsLocked,
                    UseAllTxtFiles = sub.UseAllTxtFiles,
                    SelectedTxtFile = sub.SelectedTxtFile,
                    Order = sub.Order,
                    Entries = sub.Entries
                        .OrderBy(e => e.Order)
                        .ThenBy(e => e.Name)
                        .Select(e => new EntryMeta { Name = e.Name, Enabled = e.Enabled, Order = e.Order })
                        .ToList()
                };
                File.WriteAllText(subMetaPath, JsonSerializer.Serialize(sm, _json));
            }
        }
    }
}
