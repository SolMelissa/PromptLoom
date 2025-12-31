// NOTE (PromptLoom 1.7.0):
// Design fix: filesystem is Category/SubCategory/*.txt (multiple files). Prior UI treated a subcategory as one merged file.
// This update introduces PromptEntryModel and SubCategoryModel.Entries so each .txt is managed (enable + order + select) explicitly.
// If you see compile errors referencing TxtFiles/SelectedTxtFile, switch callsites to Entries/SelectedEntry (migration helpers remain).

using System;
using System.Linq;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PromptLoom.Models;


public sealed class PromptEntryModel : INotifyPropertyChanged
{
    public string Name { get; }
    public string FilePath { get; }

    private bool _enabled = true;
    public bool Enabled { get => _enabled; set { _enabled = value; OnPropertyChanged(); } }

    private int _order;
    public int Order { get => _order; set { _order = value; OnPropertyChanged(); } }

    public PromptEntryModel(string name, string filePath)
    {
        Name = name;
        FilePath = filePath;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class SubCategoryModel : INotifyPropertyChanged
{
    public string Name { get; }
    public string FolderPath { get; }

    private string _prefix = "";
    public string Prefix { get => _prefix; set { _prefix = value; OnPropertyChanged(); } }

    private string _suffix = "";
    public string Suffix { get => _suffix; set { _suffix = value; OnPropertyChanged(); } }

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            // Expand when enabled, collapse when disabled
            IsExpanded = value;
            OnPropertyChanged();
        }
    }

    private bool _isExpanded;
    public bool IsExpanded { get => _isExpanded; set { _isExpanded = value; OnPropertyChanged(); } }

    private bool _isLocked;
    /// <summary>
    /// When true, randomization will not change this subcategory's current selection.
    /// </summary>
    public bool IsLocked { get => _isLocked; set { _isLocked = value; OnPropertyChanged(); } }

    // Default matches SchemaService/SubCategoryMeta: treat a subcategory as a collection of files.
    // Users can switch to single-file mode by setting UseAllTxtFiles=false.
    private bool _useAllTxtFiles = true;
    public bool UseAllTxtFiles { get => _useAllTxtFiles; set { _useAllTxtFiles = value; OnPropertyChanged(); } }

    private int _order;
    public int Order { get => _order; set { _order = value; OnPropertyChanged(); } }

    // Indicates that this subcategory is currently the drop target during drag-and-drop.
    private bool _isDropTarget;
    public bool IsDropTarget { get => _isDropTarget; set { _isDropTarget = value; OnPropertyChanged(); } }

    private string _currentEntry = "";
    public string CurrentEntry { get => _currentEntry; set { _currentEntry = value; OnPropertyChanged(); } }

    // UI preview of entries loaded from wildcard .txt files.
    // This is informational only (does not change prompt selection).
    public ObservableCollection<string> PreviewEntries { get; } = new();

    
    // File-level nodes under this SubCategory (mirrors *.txt in FolderPath).
    public ObservableCollection<PromptEntryModel> Entries { get; } = new();

    private PromptEntryModel? _selectedEntry;
    public PromptEntryModel? SelectedEntry { get => _selectedEntry; set { _selectedEntry = value; OnPropertyChanged(); OnPropertyChanged(nameof(SelectedTxtFile)); } }

    // Legacy compatibility: older builds stored only a single selected filename.
    // Keep this property so old configs and any leftover bindings don't crash.
    public string? SelectedTxtFile
    {
        get => SelectedEntry?.Name;
        set
        {
            var match = Entries.FirstOrDefault(e => string.Equals(e.Name, value, StringComparison.OrdinalIgnoreCase));
            SelectedEntry = match ?? Entries.FirstOrDefault();
            OnPropertyChanged();
        }
    }

private string _entrySummary = "";
    public string EntrySummary { get => _entrySummary; set { _entrySummary = value; OnPropertyChanged(); } }
    public SubCategoryModel(string name, string folderPath)
    {
        Name = name;
        FolderPath = folderPath;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed class CategoryModel : INotifyPropertyChanged
{
    public string Name { get; }
    public string FolderPath { get; }

    private bool _enabled;
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            // Expand when enabled, collapse when disabled
            IsExpanded = value;
            OnPropertyChanged();
        }
    }

    private bool _isExpanded;
    public bool IsExpanded { get => _isExpanded; set { _isExpanded = value; OnPropertyChanged(); } }

    private bool _isLocked;
    /// <summary>
    /// When true, randomization will not change any subcategory selections in this category.
    /// </summary>
    public bool IsLocked { get => _isLocked; set { _isLocked = value; OnPropertyChanged(); } }

    private int _order;
    public int Order { get => _order; set { _order = value; OnPropertyChanged(); } }

    private string _prefix = "";
    public string Prefix { get => _prefix; set { _prefix = value; OnPropertyChanged(); } }

    private string _suffix = "";
    public string Suffix { get => _suffix; set { _suffix = value; OnPropertyChanged(); } }

    public ObservableCollection<SubCategoryModel> SubCategories { get; } = new();

    public CategoryModel(string name, string folderPath)
    {
        Name = name;
        FolderPath = folderPath;
    }

    // Indicates that this category is currently the drop target during drag-and-drop.
    private bool _isDropTarget;
    public bool IsDropTarget
    {
        get => _isDropTarget;
        set
        {
            _isDropTarget = value;
            OnPropertyChanged();
        }
    }

    // When false only the selected subcategory will be used for prompt generation.
    // When true all checked/enabled subcategories will be used (default WPF behaviour).
    private bool _useAllSubCategories = true;
    public bool UseAllSubCategories
    {
        get => _useAllSubCategories;
        set
        {
            _useAllSubCategories = value;
            OnPropertyChanged();
        }
    }

    // Allows the user to pick a single subcategory for this category when UseAllSubCategories is false.
    private SubCategoryModel? _selectedSubCategory;
    public SubCategoryModel? SelectedSubCategory
    {
        get => _selectedSubCategory;
        set
        {
            if (!ReferenceEquals(_selectedSubCategory, value))
            {
                _selectedSubCategory = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
