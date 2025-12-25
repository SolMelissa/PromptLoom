// NOTE (PromptLoom 1.7.5.7):
// Build fix: PromptEngine.cs had its namespace/usings stripped during a cleanup pass.
// Restored standard Service file header with PromptLoom.Services namespace and required usings.
// This fixes CS0246 for CategoryModel and Regex.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PromptLoom.Models;

namespace PromptLoom.Services;

public sealed class PromptEngine
{
    
    private static readonly Regex WsRx = new Regex(@"[\t\r ]+", RegexOptions.Compiled);
    private static readonly Regex CommaRx = new Regex(@"\s*,\s*", RegexOptions.Compiled);

public sealed record GenerateResult(string Prompt, List<string> Messages);

    public GenerateResult Generate(IEnumerable<CategoryModel> categories, int? seed)
    {
        var msgs = new List<string>();
        var rng = seed.HasValue ? new Random(seed.Value) : Random.Shared;

        var orderedCats = categories
            .Where(c => c.Enabled)
            .OrderBy(c => c.Order)
            .ThenBy(c => c.Name)
            .ToList();

        if (orderedCats.Count == 0)
        {
            msgs.Add("No enabled categories. Check a category to include it.");
            return new GenerateResult("", msgs);
        }

        var catFragments = new List<string>();

        foreach (var cat in orderedCats)
        {
            // Negative prompts were removed from PromptLoom in 1.5.3.5.
            // If a user still has a legacy "Negative" category, ignore it to prevent confusion.
            var isLegacyNegative = string.Equals(cat.Name, "Negative", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(cat.Name, "Neg", StringComparison.OrdinalIgnoreCase)
                                   || string.Equals(cat.Name, "Negative Prompt", StringComparison.OrdinalIgnoreCase);
            if (isLegacyNegative)
            {
                msgs.Add("Legacy category 'Negative' is ignored (negative prompt support is disabled in this build).");
                continue;
            }

            // Use enabled subcategories in this category, ordered by user drag order.
            var activeSubs = cat.SubCategories
                .Where(s => s.Enabled)
                .OrderBy(s => s.Order)
                .ThenBy(s => s.Name)
                .ToList();

            if (activeSubs.Count == 0)
            {
                msgs.Add($"Category '{cat.Name}' is enabled, but no subcategories are checked.");
                continue;
            }

            var subFragments = new List<string>();
            foreach (var sub in activeSubs)
            {
                // Determine which files to use based on file-level settings.
                if (sub.Entries.Count == 0)
                {
                    msgs.Add($"Subcategory '{cat.Name}/{sub.Name}' has no .txt entries loaded (Entries collection is empty).");
                    continue;
                }

                var enabledFiles = sub.Entries
                    .OrderBy(e => e.Order)
                    .ThenBy(e => e.Name)
                    .Where(e => e.Enabled)
                    .ToList();

                // Safety: if the user has multiple files checked, treat the subcategory as multi-file mode.
                // This prevents confusing behavior where multiple checkboxes are on but only the selected file is used.
                var useAllFiles = sub.UseAllTxtFiles || enabledFiles.Count > 1;
                if (enabledFiles.Count > 1 && !sub.UseAllTxtFiles)
                    msgs.Add($"Subcategory '{cat.Name}/{sub.Name}' has multiple files checked; using them all in order.");

                List<string> entries;
                try
                {
                    if (useAllFiles)
                    {
                        // Multi-file mode: each enabled .txt is its own pool.
                        // We pick one entry per file, preserving the visible file order.
                        var files = enabledFiles.Count > 0 ? enabledFiles : sub.Entries
                            .OrderBy(e => e.Order)
                            .ThenBy(e => e.Name)
                            .ToList();

                        var picks = new List<string>();
                        foreach (var file in files)
                        {
                            var fileEntries = SchemaFileReader.LoadWildcardFile(file.FilePath);
                            if (fileEntries.Count == 0)
                            {
                                msgs.Add($"File '{cat.Name}/{sub.Name}/{file.Name}' has no lines.");
                                continue;
                            }

                            picks.Add(fileEntries[rng.Next(fileEntries.Count)]);
                        }

                        entries = picks;
                    }
                    else
                    {
                        var selected = sub.SelectedEntry
                                       ?? enabledFiles.FirstOrDefault()
                                       ?? sub.Entries.FirstOrDefault();
                        if (selected is null)
                        {
                            msgs.Add($"Subcategory '{cat.Name}/{sub.Name}' has no selectable entry file.");
                            continue;
                        }

                        // In single-file mode, prefer an enabled file. If the selected entry is disabled,
                        // fall back to an enabled entry without mutating checkbox state.
                        var pickFile = selected.Enabled ? selected : (enabledFiles.FirstOrDefault() ?? selected);
                        if (!pickFile.Enabled)
                            msgs.Add($"Subcategory '{cat.Name}/{sub.Name}' is in single-file mode but the selected file is disabled; using it anyway.");

                        entries = SchemaFileReader.LoadWildcardFile(pickFile.FilePath);
                    }
                }
                catch (Exception ex)
                {
                    msgs.Add($"Subcategory '{cat.Name}/{sub.Name}' couldn't load entries: {ex.Message}");
                    continue;
                }

                if (entries.Count == 0)
                {
                    msgs.Add($"Subcategory '{cat.Name}/{sub.Name}' has no entries.");
                    continue;
                }

                // Single-file mode returns a pool; multi-file mode returns one pick per file (already ordered).
                // Locks: if the category or subcategory is locked, we reuse the previously picked value (when present)
                // so repeated randomizations don't mutate the prompt state.
                string Roll()
                    => useAllFiles ? string.Join(", ", entries) : entries[rng.Next(entries.Count)];

                var locked = cat.IsLocked || sub.IsLocked;
                var chosen = locked && !string.IsNullOrWhiteSpace(sub.CurrentEntry)
                    ? sub.CurrentEntry
                    : Roll();

                // Track what was picked so the UI can show "current" per subcategory.
                // If locked but empty, we allow a one-time initialization.
                if (!locked || string.IsNullOrWhiteSpace(sub.CurrentEntry))
                    sub.CurrentEntry = chosen;
                var frag = WrapWithAffixes(chosen, sub.Prefix, sub.Suffix);
                if (!string.IsNullOrWhiteSpace(frag))
                    subFragments.Add(frag);
            }

            if (subFragments.Count == 0) continue;

            var inner = string.Join(" ", subFragments);
            var catFrag = WrapWithAffixes(inner, cat.Prefix, cat.Suffix);
            if (!string.IsNullOrWhiteSpace(catFrag))
            {
                catFragments.Add(catFrag);
            }
        }

        // Line breaks between main category segments for readability.
        // Normalize fragments individually, but preserve newlines between categories.
        var prompt = string.Join("\n", catFragments.Select(Normalize));

        if (string.IsNullOrWhiteSpace(prompt))
            msgs.Add("Prompt is empty. Add entries to your wildcard .txt files and ensure subcategories are checked.");

        return new GenerateResult(prompt, msgs);
    }

    private static string WrapWithAffixes(string core, string? prefix, string? suffix)
    {
        var p = (prefix ?? "").Trim();
        var s = (suffix ?? "").Trim();
        var c = (core ?? "").Trim();

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(p)) parts.Add(p);
        if (!string.IsNullOrWhiteSpace(c)) parts.Add(c);
        if (!string.IsNullOrWhiteSpace(s)) parts.Add(s);

        return Normalize(string.Join(" ", parts));
    }

    private static string Normalize(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        // Do not collapse newlines here, as the outer join uses them intentionally.
        var cleaned = WsRx.Replace(s, " ").Trim();
        cleaned = CommaRx.Replace(cleaned, ", ");
        return cleaned;
    }
}
