// CHANGE LOG
// - 2026-03-05 | Request: Tag selection provider | Build prompt selection from ordered selected files.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using PromptLoom.Models;
using PromptLoom.Services;

namespace PromptLoom.ViewModels;

/// <summary>
/// Selection provider that uses the ordered file selection from tag search.
/// </summary>
public sealed class SelectedFilesSelectionProvider : IPromptSelectionProvider
{
    private readonly ObservableCollection<CategoryModel> _categories;
    private readonly ObservableCollection<TagFileInfo> _selectedFiles;

    public SelectedFilesSelectionProvider(
        ObservableCollection<CategoryModel> categories,
        ObservableCollection<TagFileInfo> selectedFiles)
    {
        _categories = categories ?? throw new ArgumentNullException(nameof(categories));
        _selectedFiles = selectedFiles ?? throw new ArgumentNullException(nameof(selectedFiles));
    }

    public IReadOnlyList<CategoryModel> GetSelection()
    {
        if (_selectedFiles.Count == 0)
            return Array.Empty<CategoryModel>();

        var fileLookup = BuildEntryLookup(_categories);
        var categoryBuilders = new Dictionary<string, CategoryBuilder>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < _selectedFiles.Count; i++)
        {
            var selected = _selectedFiles[i];
            if (!fileLookup.TryGetValue(selected.Path, out var source))
                continue;

            if (!categoryBuilders.TryGetValue(source.Category.Name, out var categoryBuilder))
            {
                categoryBuilder = new CategoryBuilder(source.Category, i);
                categoryBuilders[source.Category.Name] = categoryBuilder;
            }
            else
            {
                categoryBuilder.MinOrder = Math.Min(categoryBuilder.MinOrder, i);
            }

            categoryBuilder.AddEntry(source, i);
        }

        var categories = categoryBuilders.Values
            .OrderBy(builder => builder.MinOrder)
            .ThenBy(builder => builder.Category.Name, StringComparer.OrdinalIgnoreCase)
            .Select(builder => builder.Build())
            .ToList();

        for (var i = 0; i < categories.Count; i++)
            categories[i].Order = i;

        return categories;
    }

    private static Dictionary<string, SourceEntry> BuildEntryLookup(IEnumerable<CategoryModel> categories)
    {
        var lookup = new Dictionary<string, SourceEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var category in categories)
        {
            foreach (var sub in category.SubCategories)
            {
                foreach (var entry in sub.Entries)
                    lookup[entry.FilePath] = new SourceEntry(category, sub, entry);
            }
        }

        return lookup;
    }

    private sealed record SourceEntry(CategoryModel Category, SubCategoryModel SubCategory, PromptEntryModel Entry);

    private sealed class CategoryBuilder
    {
        private readonly Dictionary<string, SubCategoryBuilder> _subCategories =
            new(StringComparer.OrdinalIgnoreCase);

        public CategoryBuilder(CategoryModel source, int initialOrder)
        {
            Source = source;
            MinOrder = initialOrder;
            Category = new CategoryModel(source.Name, source.FolderPath)
            {
                Prefix = source.Prefix,
                Suffix = source.Suffix,
                Enabled = true,
                IsLocked = source.IsLocked,
                UseAllSubCategories = true,
                Order = initialOrder
            };
        }

        public CategoryModel Source { get; }
        public CategoryModel Category { get; }
        public int MinOrder { get; set; }

        public void AddEntry(SourceEntry source, int order)
        {
            if (!_subCategories.TryGetValue(source.SubCategory.Name, out var subBuilder))
            {
                subBuilder = new SubCategoryBuilder(source.SubCategory, order);
                _subCategories[source.SubCategory.Name] = subBuilder;
            }
            else
            {
                subBuilder.MinOrder = Math.Min(subBuilder.MinOrder, order);
            }

            subBuilder.AddEntry(source.Entry, order);
        }

        public CategoryModel Build()
        {
            var subs = _subCategories.Values
                .OrderBy(builder => builder.MinOrder)
                .ThenBy(builder => builder.SubCategory.Name, StringComparer.OrdinalIgnoreCase)
                .Select(builder => builder.Build())
                .ToList();

            for (var i = 0; i < subs.Count; i++)
                subs[i].Order = i;

            foreach (var sub in subs)
                Category.SubCategories.Add(sub);

            return Category;
        }
    }

    private sealed class SubCategoryBuilder
    {
        private readonly List<(PromptEntryModel Entry, int Order)> _entries = new();

        public SubCategoryBuilder(SubCategoryModel source, int initialOrder)
        {
            MinOrder = initialOrder;
            SubCategory = new SubCategoryModel(source.Name, source.FolderPath)
            {
                Prefix = source.Prefix,
                Suffix = source.Suffix,
                Enabled = true,
                IsLocked = source.IsLocked,
                UseAllTxtFiles = true,
                Order = initialOrder
            };
        }

        public SubCategoryModel SubCategory { get; }
        public int MinOrder { get; set; }

        public void AddEntry(PromptEntryModel source, int order)
        {
            var entry = new PromptEntryModel(source.Name, source.FilePath)
            {
                Enabled = true,
                Order = order
            };

            _entries.Add((entry, order));
        }

        public SubCategoryModel Build()
        {
            var ordered = _entries
                .OrderBy(item => item.Order)
                .ThenBy(item => item.Entry.Name, StringComparer.OrdinalIgnoreCase)
                .Select((item, index) =>
                {
                    item.Entry.Order = index;
                    return item.Entry;
                })
                .ToList();

            foreach (var entry in ordered)
                SubCategory.Entries.Add(entry);

            SubCategory.SelectedEntry = ordered.FirstOrDefault();
            return SubCategory;
        }
    }
}
