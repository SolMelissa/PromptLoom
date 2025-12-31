// NOTE (PromptLoom 1.7.0):
// Adds file-level metadata (entries[]) under each subcategory so individual .txt files can be enabled and ordered.
// Backward compatible: older configs without entries[] will be migrated in SchemaService.Load().

namespace PromptLoom.Models;

public sealed class EntryMeta
{
    public string Name { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public int Order { get; set; } = 0;
}

public sealed class CategoryMeta
{
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";
    public bool Enabled { get; set; } = false;
    public bool IsLocked { get; set; } = false;
    public int Order { get; set; } = 0;
}

public sealed class SubCategoryMeta
{
    // New in 1.7.0: per-file settings.
    public System.Collections.Generic.List<EntryMeta>? Entries { get; set; } = null;

    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";
    public bool Enabled { get; set; } = false; // included in prompt
    public bool IsLocked { get; set; } = false;
    // Default behavior should match the mental model of a SubCategory being a collection.
    // In 1.7.0 this means: include all enabled files under the subcategory unless the user explicitly
    // switches to single-file mode.
    public bool UseAllTxtFiles { get; set; } = true;
    public string? SelectedTxtFile { get; set; } = null;
    public int Order { get; set; } = 0;
}
