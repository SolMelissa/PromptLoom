// FIX: Introduce UI side-effect wrappers for MessageBox/Clipboard/Process/Dispatcher to improve testability.
// CAUSE: Direct static UI calls in the view model required WPF runtime in tests.
// CHANGE: Inject UI service wrappers and use them for side effects. 2025-12-25
// FIX: build warning CS1998 (async method without await) in SendToSwarmUi | CAUSE: async void wrapper without awaits
// CHANGE: make SendToSwarmUi synchronous and keep fire-and-forget generation | DATE: 2025-12-25
// FIX: Allow core dependencies (file/time/random) to be injected for tests.
// CAUSE: MainViewModel hard-coded static services and Random.Shared, making deterministic tests difficult.
// CHANGE: Add injectable IWildcardFileReader, IClock, and IRandomSource. 2025-12-25
// FIX: build warning CS1998 (async method without await) in Copy | CAUSE: async void with no awaited work
// CHANGE: make Copy synchronous and remove dummy await | DATE: 2025-12-25
// FIX: runtime crash "Parameter count mismatch" when recomputing prompt via Dispatcher.Invoke | CAUSE: RecomputePrompt has optional parameter and was invoked as a delegate with no args
// CHANGE: invoke RecomputePrompt through a parameterless lambda | DATE: 2025-12-25
// FIX: build error CS0103 (AutoSave missing) when randomizing categories/subcategories | CAUSE: refactor removed helper used in event handlers
// CHANGE: reintroduce AutoSave helper to funnel into debounced queue | DATE: 2026-03-02
// FIX: build error CS1039 (Unterminated string literal) in TestSwarmConnection | CAUSE: multiline interpolated string split across lines
// CHANGE: keep the string on one source line using \n escape | DATE: 2025-12-22
// FIX: build error CS0103 (missing TryParseSwarmWsFrame symbol) | CAUSE: parser helper exists as SwarmWsParser.TryParseSwarmWsFrame but was called unqualified
// CHANGE: call SwarmWsParser.TryParseSwarmWsFrame explicitly | DATE: 2025-12-22
// FIX: UX regression (Send-to-SwarmUI button text changed / implied single-flight) | CAUSE: SendToSwarmUi mutated SwarmButtonText and ran a "Sent!" timer
// CHANGE: keep button text stable; allow multiple overlapping sends and track status per-thumbnail | DATE: 2025-12-22

// NOTE (PromptLoom 1.8.0.1):
// SwarmUI integration: added a SwarmUI bridge reference and a "Send Prompt to SwarmUI" button/command.
// IMPORTANT: PromptLoom intentionally does NOT send model/resolution/steps/sampler so SwarmUI's current UI settings remain authoritative.
//
// NOTE (PromptLoom 1.7.0):
// Adds file-level selection (PromptEntryModel) under each subcategory and a simple in-app editor for the selected .txt file.
// Fixes the design mismatch where multiple files were implicitly merged into a single subcategory pool in the UI.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Text;
using System.Globalization;
using PromptLoom.Models;
using PromptLoom.Services;
using SwarmUi.Client;

namespace PromptLoom.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly string _installDir;
    private readonly SchemaService _schema;
    private readonly PromptEngine _engine;
    private readonly IWildcardFileReader _fileReader;
    private readonly IClock _clock;
    private readonly IRandomSource _randomSource;
    private readonly Random _random;
    private readonly IUiDialogService _uiDialog;
    private readonly IClipboardService _clipboard;
    private readonly IProcessService _process;
    private readonly IDispatcherService _dispatcher;

    private readonly ErrorReporter _errors = ErrorReporter.Instance;

    // Reuse a single HttpClient instance for image downloads.
    private static readonly HttpClient s_http = new HttpClient();

    public ObservableCollection<string> ErrorEntries => _errors.Entries;

    // Bulk updates (All/None) must not trigger mid-loop prompt recomputes.
    private int _bulkUpdateDepth;

    // Debounce prompt recomputation to avoid rebuilding several times per UI gesture.
    private CancellationTokenSource? _recomputeCts;
    private readonly TimeSpan _recomputeDebounce = TimeSpan.FromMilliseconds(90);

    private CancellationTokenSource? _autoSaveCts;
    private readonly TimeSpan _autoSaveDebounce = TimeSpan.FromMilliseconds(800);

    public ObservableCollection<CategoryModel> Categories { get; } = new();

    private string _copyButtonText = "Copy";
    public string CopyButtonText
    {
        get => _copyButtonText;
        private set { _copyButtonText = value; OnPropertyChanged(); }
    }

    private string _swarmButtonText = "Send Prompt to SwarmUI";
    public string SwarmButtonText
    {
        get => _swarmButtonText;
        private set { _swarmButtonText = value; OnPropertyChanged(); }
    }

    private bool _isSwarmGenerating;
    public bool IsSwarmGenerating
    {
        get => _isSwarmGenerating;
        private set { _isSwarmGenerating = value; OnPropertyChanged(); }
    }

    private string _swarmGenerationStatus = "";
    public string SwarmGenerationStatus
    {
        get => _swarmGenerationStatus;
        private set { _swarmGenerationStatus = value; OnPropertyChanged(); }
    }

    private ImageSource? _latestGeneratedImage;
    public ImageSource? LatestGeneratedImage
    {
        get => _latestGeneratedImage;
        private set { _latestGeneratedImage = value; OnPropertyChanged(); }
    }


    public ObservableCollection<RecentSwarmBatchViewModel> RecentBatches { get; } = new();

    // SwarmUI connection default (local SwarmUI default port).
    private string _swarmUrl = "http://127.0.0.1:7801";
    public string SwarmUrl
    {
        get => _swarmUrl;
        set { _swarmUrl = value; OnPropertyChanged(); QueueAutoSave(); }
    }

    private string _swarmToken = "";
    public string SwarmToken
    {
        get => _swarmToken;
        set { _swarmToken = value; OnPropertyChanged(); QueueAutoSave(); }
    }

    private string _swarmSelectedModel = "";
    public string SwarmSelectedModel
    {
        get => _swarmSelectedModel;
        set { _swarmSelectedModel = value; OnPropertyChanged(); }
    }

    public ObservableCollection<string> SwarmModels { get; } = new();

    public ObservableCollection<string> SwarmLoras { get; } = new();

    private bool _sendSwarmModelOverride = true;
    public bool SendSwarmModelOverride
    {
        get => _sendSwarmModelOverride;
        set { _sendSwarmModelOverride = value; OnPropertyChanged(); QueueAutoSave(); OnPropertyChanged(nameof(CanEditSwarmModelOverride)); }
    }
    public bool CanEditSwarmModelOverride => SendSwarmModelOverride;

    private bool _sendSwarmSteps = false;
    public bool SendSwarmSteps
    {
        get => _sendSwarmSteps;
        set { _sendSwarmSteps = value; OnPropertyChanged(); QueueAutoSave(); OnPropertyChanged(nameof(CanEditSwarmSteps)); }
    }
    public bool CanEditSwarmSteps => SendSwarmSteps;

    private bool _sendSwarmCfgScale = false;
    public bool SendSwarmCfgScale
    {
        get => _sendSwarmCfgScale;
        set { _sendSwarmCfgScale = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanEditSwarmCfgScale)); }
    }
    public bool CanEditSwarmCfgScale => SendSwarmCfgScale;

    private int _swarmSteps = 30;
    public int SwarmSteps
    {
        get => _swarmSteps;
        set { _swarmSteps = Math.Max(1, value); OnPropertyChanged(); QueueAutoSave(); }
    }

    private double _swarmCfgScale = 6.5;
    public double SwarmCfgScale
    {
        get => _swarmCfgScale;
        set { _swarmCfgScale = Math.Max(0.0, value); OnPropertyChanged(); QueueAutoSave(); }
    }

    private bool _sendSwarmLoras = false;
    public bool SendSwarmLoras
    {
        get => _sendSwarmLoras;
        set { _sendSwarmLoras = value; OnPropertyChanged(); QueueAutoSave(); OnPropertyChanged(nameof(CanEditSwarmLoras)); }
    }
    public bool CanEditSwarmLoras => SendSwarmLoras;

    private string _swarmSelectedLora1 = "";
    public string SwarmSelectedLora1
    {
        get => _swarmSelectedLora1;
        set { _swarmSelectedLora1 = value; OnPropertyChanged(); QueueAutoSave(); }
    }

    private string _swarmSelectedLora2 = "";
    public string SwarmSelectedLora2
    {
        get => _swarmSelectedLora2;
        set { _swarmSelectedLora2 = value; OnPropertyChanged(); }
    }

    private double _swarmLora1Weight = 1.0;
    public double SwarmLora1Weight
    {
        get => _swarmLora1Weight;
        set { _swarmLora1Weight = value; OnPropertyChanged(); QueueAutoSave(); }
    }

    private double _swarmLora2Weight = 1.0;
    public double SwarmLora2Weight
    {
        get => _swarmLora2Weight;
        set { _swarmLora2Weight = value; OnPropertyChanged(); QueueAutoSave(); }
    }

    private int _batchQty = 4;
    public int BatchQty
    {
        get => _batchQty;
        set { _batchQty = Math.Clamp(value, 1, 999); OnPropertyChanged(); QueueAutoSave(); }
    }

    private bool _batchRandomizePrompts = true;
    public bool BatchRandomizePrompts
    {
        get => _batchRandomizePrompts;
        set { _batchRandomizePrompts = value; OnPropertyChanged(); QueueAutoSave(); }
    }

    private bool _isImageOverlayVisible;
    public bool IsImageOverlayVisible
    {
        get => _isImageOverlayVisible;
        set { _isImageOverlayVisible = value; OnPropertyChanged(); }
    }

    private ImageSource? _overlayImage;
    public ImageSource? OverlayImage
    {
        get => _overlayImage;
        set { _overlayImage = value; OnPropertyChanged(); }
    }

    private RecentSwarmImageViewModel? _overlaySelectedItem;
    public RecentSwarmImageViewModel? OverlaySelectedItem
    {
        get => _overlaySelectedItem;
        set { _overlaySelectedItem = value; OnPropertyChanged(); }
    }

    private bool _overlayDetailsExpanded;
    public bool OverlayDetailsExpanded
    {
        get => _overlayDetailsExpanded;
        set { _overlayDetailsExpanded = value; OnPropertyChanged(); }
    }

    private string _swarmUiStatusText = "Not connected yet.";
    public string SwarmUiStatusText
    {
        get => _swarmUiStatusText;
        set { _swarmUiStatusText = value; OnPropertyChanged(); }
    }

    private DispatcherTimer? _swarmResetTimer;

    private DispatcherTimer? _copyResetTimer;

    // WPF SelectedItem bindings on both the left ListBox and the center TabControl are TwoWay by default.
    // If we try to keep SelectedCategory and ActiveCategory in sync naively, WPF can re-enter setters
    // and trigger an uncatchable StackOverflowException (the process just exits).
    private bool _syncingCategorySelection;
    private bool _syncingSubCategorySelection;

    // Prevent re-entrant preview refresh loops.
    // Updating EntrySummary during RefreshSubCategoryPreview raises PropertyChanged,
    // which can otherwise cause OnSubCategoryPropertyChanged -> RefreshSelectedPreview ->
    // RefreshSubCategoryPreview... and eventually a StackOverflow (hard process exit).
    private bool _refreshingPreview;

    /// <summary>
    /// Category currently being edited in the center panel (tabs).
    /// </summary>
    private CategoryModel? _activeCategory;
    public CategoryModel? ActiveCategory
    {
        get => _activeCategory;
        set
        {
            if (ReferenceEquals(_activeCategory, value)) return;
            _activeCategory = value;
            OnPropertyChanged();

            // Keep left selection in sync with the editor.
            if (!_syncingCategorySelection)
            {
                try
                {
                    _syncingCategorySelection = true;
                    if (_activeCategory is not null && !ReferenceEquals(SelectedCategory, _activeCategory))
                        SelectedCategory = _activeCategory;
                }
                finally
                {
                    _syncingCategorySelection = false;
                }
            }

            // When switching tabs, move editing focus to that category's selected (or first) subcategory.
            if (_activeCategory is not null)
            {
                var newSub = _activeCategory.SelectedSubCategory ?? _activeCategory.SubCategories.FirstOrDefault();
                if (!ReferenceEquals(SelectedSubCategory, newSub))
                    SelectedSubCategory = newSub;
            }
        }
    }

    private CategoryModel? _selectedCategory;
    public CategoryModel? SelectedCategory
    {
        get => _selectedCategory;
        set
        {
            if (ReferenceEquals(_selectedCategory, value)) return;
            _selectedCategory = value;
            OnPropertyChanged();

            // If the user clicked a category on the left panel, switch the editor tab too.
            if (!_syncingCategorySelection)
            {
                try
                {
                    _syncingCategorySelection = true;
                    if (_selectedCategory is not null && !ReferenceEquals(ActiveCategory, _selectedCategory))
                        ActiveCategory = _selectedCategory;
                }
                finally
                {
                    _syncingCategorySelection = false;
                }
            }

            // When changing selected category, update the selected subcategory to that category's selected or first
            if (_selectedCategory is not null)
            {
                var newSub = _selectedCategory.SelectedSubCategory ?? _selectedCategory.SubCategories.FirstOrDefault();
                if (!ReferenceEquals(SelectedSubCategory, newSub))
                    SelectedSubCategory = newSub;
            }
        }
    }

    private SubCategoryModel? _selectedSubCategory;
    public SubCategoryModel? SelectedSubCategory
    {
        get => _selectedSubCategory;
        set
        {
            if (ReferenceEquals(_selectedSubCategory, value)) return;
            _selectedSubCategory = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSubCategoryDisplayName));

            // When a subcategory is selected for editing, also mark it as the selected subcategory for its parent category.
            if (value is not null && !_syncingSubCategorySelection)
            {
                try
                {
                    _syncingSubCategorySelection = true;
                    var parent = Categories.FirstOrDefault(c => c.SubCategories.Contains(value));
                    if (parent is not null && !ReferenceEquals(parent.SelectedSubCategory, value))
                        parent.SelectedSubCategory = value;
                }
                finally
                {
                    _syncingSubCategorySelection = false;
                }
            }

            RefreshSelectedPreview();

            // Keep file selection in sync: when subcategory changes, default to its selected/first file.
            if (SelectedSubCategory is not null)
                SelectedEntry = SelectedSubCategory.SelectedEntry ?? SelectedSubCategory.Entries.FirstOrDefault();

            QueueRecomputePrompt();
        }
    }

    public string SelectedSubCategoryDisplayName
        => SelectedSubCategory is null ? "(no subcategory selected)" : $"{FindParentCategoryName(SelectedSubCategory)}/{SelectedSubCategory.Name}";

    
    private PromptEntryModel? _selectedEntry;
    public PromptEntryModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (ReferenceEquals(_selectedEntry, value)) return;
            _selectedEntry = value;

            // Keep the selected file stored on the owning SubCategoryModel so prompt generation
            // (PromptEngine) and persistence (_subcategory.json) use the same selection.
            // This fixes a 1.7.0 regression where the UI selection only updated MainViewModel.SelectedEntry.
            if (SelectedSubCategory is not null && value is not null && SelectedSubCategory.Entries.Contains(value))
                SelectedSubCategory.SelectedEntry = value;

            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedEntryDisplayName));
            LoadSelectedEntryFile();
        }
    }

    public string SelectedEntryDisplayName
        => SelectedEntry is null ? "(no file selected)" : $"{FindParentCategoryName(SelectedSubCategory ?? new SubCategoryModel("?", "?"))}/{SelectedSubCategory?.Name}/{SelectedEntry.Name}";

    private string _entryEditorText = "";
    public string EntryEditorText
    {
        get => _entryEditorText;
        set { _entryEditorText = value; OnPropertyChanged(); }
    }

    private string _entryEditorPath = "";
    public string EntryEditorPath
    {
        get => _entryEditorPath;
        private set { _entryEditorPath = value; OnPropertyChanged(); }
    }

private string _promptText = "";
    public string PromptText { get => _promptText; set { _promptText = value; OnPropertyChanged(); } }

    private string _seedText = "";
    // NOTE (2025-12-25): SeedText is user-editable and normally triggers prompt recomputation.
    // SwarmUI can also report back the *actual* seed used for a generation; updating the seed display
    // should not mutate the prompt currently shown in the prompt box. We therefore support a
    // suppression mode for programmatic updates coming from SwarmUI frames.
    public string SeedText
    {
        get => _seedText;
        set
        {
            _seedText = value;
            OnPropertyChanged();
            if (!_suppressSeedDrivenPromptRecompute)
                RecomputePrompt();
        }
    }

    private bool _suppressSeedDrivenPromptRecompute;

    private void SetSeedTextFromSwarm(long seed)
    {
        _suppressSeedDrivenPromptRecompute = true;
        try
        {
            SeedText = seed.ToString(CultureInfo.InvariantCulture);
        }
        finally
        {
            _suppressSeedDrivenPromptRecompute = false;
        }
    }

    // (UI) Copy feedback is shown directly on the Copy button.

    private string _systemMessages = "";
    public string SystemMessages { get => _systemMessages; set { _systemMessages = value; OnPropertyChanged(); } }

    // Legacy: kept for compatibility with earlier builds. The 1.5.1 UI shows previews per-subcategory.
    public ObservableCollection<string> SelectedSubCategoryPreview { get; } = new();
    private string _selectedSubCategoryEntrySummary = "";
    public string SelectedSubCategoryEntrySummary { get => _selectedSubCategoryEntrySummary; set { _selectedSubCategoryEntrySummary = value; OnPropertyChanged(); } }

    public RelayCommand ReloadCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand CopyCommand { get; }
    public RelayCommand SendToSwarmUiCommand { get; }
    public RelayCommand SendBatchToSwarmUiCommand { get; }
    public RelayCommand RandomizeCommand { get; }
    public RelayCommand TestSwarmConnectionCommand { get; }
    public RelayCommand RefreshSwarmModelsCommand { get; }
    public RelayCommand RefreshSwarmLorasCommand { get; }
    public RelayCommand ShowImageOverlayCommand { get; }
    public RelayCommand HideImageOverlayCommand { get; }
    public RelayCommand ToggleOverlayDetailsCommand { get; }
    public RelayCommand OverlayPrevCommand { get; }
    public RelayCommand OverlayNextCommand { get; }
    public RelayCommand OpenSwarmUiCommand { get; }
    public RelayCommand SaveOutputCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand RestoreOriginalCategoriesCommand { get; }
    public RelayCommand SelectSubCategoryCommand { get; }
    public RelayCommand SelectEntryCommand { get; }
    public RelayCommand SelectAllSubCategoriesCommand { get; }
    public RelayCommand SelectNoneSubCategoriesCommand { get; }
    public RelayCommand SelectAllEntriesCommand { get; }
    public RelayCommand SelectNoneEntriesCommand { get; }
    public RelayCommand MoveCategoryUpCommand { get; }
    public RelayCommand MoveCategoryDownCommand { get; }
    public RelayCommand MoveSubCategoryUpCommand { get; }
    public RelayCommand MoveSubCategoryDownCommand { get; }
    public RelayCommand MoveEntryUpCommand { get; }
    public RelayCommand MoveEntryDownCommand { get; }
    public RelayCommand SaveEntryFileCommand { get; }
    public RelayCommand OpenEntryFileCommand { get; }

    public RelayCommand RandomizeCategoryOnceCommand { get; }
    public RelayCommand RandomizeSubCategoryOnceCommand { get; }

    /// <summary>
    /// Window-driven initialization entrypoint.
    /// We intentionally do not touch disk or mutate UI-bound collections inside the constructor,
    /// because bindings may not be fully established yet.
    /// </summary>
    public void Initialize()
    {
        Reload();

        // Load Swarm server metadata on startup (best effort).
        // Runs async void methods which update observable collections on the UI thread.
        RefreshSwarmModels();
        RefreshSwarmLoras();
    }

    /// <summary>
    /// Creates a new main view model with optional injected services.
    /// </summary>
    public MainViewModel(
        IWildcardFileReader? fileReader = null,
        IClock? clock = null,
        IRandomSource? randomSource = null,
        PromptEngine? engine = null,
        IUiDialogService? uiDialog = null,
        IClipboardService? clipboard = null,
        IProcessService? process = null,
        IDispatcherService? dispatcher = null)
    {
        _errors.Info("MainViewModel ctor begin");

        _fileReader = fileReader ?? new WildcardFileReader();
        _clock = clock ?? new SystemClock();
        _randomSource = randomSource ?? new SystemRandomSource();
        _random = _randomSource.Create(null);
        _engine = engine ?? new PromptEngine(_fileReader, _randomSource);
        _uiDialog = uiDialog ?? new UiDialogService();
        _clipboard = clipboard ?? new ClipboardService();
        _process = process ?? new ProcessService();
        _dispatcher = dispatcher ?? new DispatcherService();

        _installDir = FindInstallDir();
        _errors.Info("Resolved InstallDir: " + _installDir);

        // Persist Categories + Outputs across versions.
        AppDataStore.EnsureInitialized(_installDir, _errors);
        var root = AppDataStore.RootDir;
        _errors.Info("Using AppData RootDir: " + root);

        _schema = new SchemaService(root);
        _errors.Info("SchemaService created. CategoriesDir=" + _schema.CategoriesDir);

        ReloadCommand = new RelayCommand("Reload", _ => Reload());
        SaveCommand = new RelayCommand("Save", _ => Save());
        CopyCommand = new RelayCommand("Copy", _ => Copy());
        SendToSwarmUiCommand = new RelayCommand("SendToSwarmUi", _ => SendToSwarmUi());
        SendBatchToSwarmUiCommand = new RelayCommand("SendBatchToSwarmUi", _ => SendBatchToSwarmUi());
        RandomizeCommand = new RelayCommand("Randomize", _ => Randomize());
        TestSwarmConnectionCommand = new RelayCommand("TestSwarmConnection", _ => TestSwarmConnection());
        RefreshSwarmModelsCommand = new RelayCommand("RefreshSwarmModels", _ => RefreshSwarmModels());
        RefreshSwarmLorasCommand = new RelayCommand("RefreshSwarmLoras", _ => RefreshSwarmLoras());
        ShowImageOverlayCommand = new RelayCommand("ShowImageOverlay", p => ShowImageOverlay(p));
        HideImageOverlayCommand = new RelayCommand("HideImageOverlay", _ => HideImageOverlay());
        ToggleOverlayDetailsCommand = new RelayCommand("ToggleOverlayDetails", _ => OverlayDetailsExpanded = !OverlayDetailsExpanded);
        OverlayPrevCommand = new RelayCommand("OverlayPrev", _ => NavigateOverlay(-1));
        OverlayNextCommand = new RelayCommand("OverlayNext", _ => NavigateOverlay(+1));
        OpenSwarmUiCommand = new RelayCommand("OpenSwarmUi", _ => OpenSwarmUi());
        SaveOutputCommand = new RelayCommand("SaveOutput", _ => SaveOutput());
        OpenFolderCommand = new RelayCommand("OpenFolder", _ => OpenFolder());
        RestoreOriginalCategoriesCommand = new RelayCommand("RestoreOriginalCategories", _ => RestoreOriginalCategories());
        SelectSubCategoryCommand = new RelayCommand("SelectSubCategory", p => SelectSubCategory(p));
        SelectEntryCommand = new RelayCommand("SelectEntry", p => SelectEntry(p));
        SelectAllSubCategoriesCommand = new RelayCommand("SelectAllSubCategories", p => SelectAllSubCategories(p));
        SelectNoneSubCategoriesCommand = new RelayCommand("SelectNoneSubCategories", p => SelectNoneSubCategories(p));
        SelectAllEntriesCommand = new RelayCommand("SelectAllEntries", p => SelectAllEntries(p));
        SelectNoneEntriesCommand = new RelayCommand("SelectNoneEntries", p => SelectNoneEntries(p));

        MoveCategoryUpCommand = new RelayCommand("MoveCategoryUp", p => MoveCategoryUp(p));
        MoveCategoryDownCommand = new RelayCommand("MoveCategoryDown", p => MoveCategoryDown(p));
        MoveSubCategoryUpCommand = new RelayCommand("MoveSubCategoryUp", p => MoveSubCategoryUp(p));
        MoveSubCategoryDownCommand = new RelayCommand("MoveSubCategoryDown", p => MoveSubCategoryDown(p));

        MoveEntryUpCommand = new RelayCommand("MoveEntryUp", p => MoveEntryUp(p));
        MoveEntryDownCommand = new RelayCommand("MoveEntryDown", p => MoveEntryDown(p));
        SaveEntryFileCommand = new RelayCommand("SaveEntryFile", _ => SaveEntryFile());
        OpenEntryFileCommand = new RelayCommand("OpenEntryFile", _ => OpenEntryFile());

        RandomizeCategoryOnceCommand = new RelayCommand("RandomizeCategoryOnce", p => RandomizeCategoryOnce(p));
        RandomizeSubCategoryOnceCommand = new RelayCommand("RandomizeSubCategoryOnce", p => RandomizeSubCategoryOnce(p));

        _errors.Info("MainViewModel ctor end");
    
        LoadUserSettings();
}

    private void RandomizeCategoryOnce(object? param)
    {
        if (param is not CategoryModel cat) return;

        // Preserve current prompt state by locking everything except the target category.
        var originalCatLocks = Categories.ToDictionary(c => c, c => c.IsLocked);
        var originalSubLocks = Categories.SelectMany(c => c.SubCategories).ToDictionary(s => s, s => s.IsLocked);

        try
        {
            foreach (var c in Categories)
            {
                if (!ReferenceEquals(c, cat))
                    c.IsLocked = true;
                foreach (var s in c.SubCategories)
                    s.IsLocked = true;
            }

            cat.IsLocked = false;
            foreach (var s in cat.SubCategories)
            {
                // respect per-sub locks; if the user locked a subcategory, keep it fixed even when rolling the category.
                s.IsLocked = originalSubLocks.TryGetValue(s, out var locked) && locked;
                if (!s.IsLocked) s.CurrentEntry = "";
            }

            RecomputePrompt(seedOverride: _random.Next(0, int.MaxValue));
            AutoSave();
        }
        finally
        {
            foreach (var kv in originalCatLocks) kv.Key.IsLocked = kv.Value;
            foreach (var kv in originalSubLocks) kv.Key.IsLocked = kv.Value;
        }
    }

    private void RandomizeSubCategoryOnce(object? param)
    {
        if (param is not SubCategoryModel sub) return;

        var originalCatLocks = Categories.ToDictionary(c => c, c => c.IsLocked);
        var originalSubLocks = Categories.SelectMany(c => c.SubCategories).ToDictionary(s => s, s => s.IsLocked);

        try
        {
            foreach (var c in Categories)
            {
                c.IsLocked = true;
                foreach (var s in c.SubCategories)
                    s.IsLocked = true;
            }

            sub.IsLocked = false;
            sub.CurrentEntry = "";

            RecomputePrompt(seedOverride: _random.Next(0, int.MaxValue));
            AutoSave();
        }
        finally
        {
            foreach (var kv in originalCatLocks) kv.Key.IsLocked = kv.Value;
            foreach (var kv in originalSubLocks) kv.Key.IsLocked = kv.Value;
        }
    }

    /// <summary>
    /// Finds the most likely app root folder containing the Categories directory.
    /// This helps when running under dotnet run where BaseDirectory points at bin/Debug.
    /// </summary>
    private static string FindInstallDir()
    {
        var current = AppContext.BaseDirectory;
        for (var i = 0; i < 6; i++)
        {
            // Strong signals we are at the app root.
            // When running from an extracted release zip, Categories lives next to the exe.
            // When running under dotnet run, Categories lives at the project root and we need to walk upward.
            var csproj = Path.Combine(current, "PromptLoom.csproj");
            if (File.Exists(csproj))
                return current;

            var categoriesDir = Path.Combine(current, "Categories");
            if (Directory.Exists(categoriesDir))
            {
                // Heuristic: if Categories has any content (folders/files) OR any _category.json,
                // we assume this is the right root.
                try
                {
                    if (Directory.EnumerateFileSystemEntries(categoriesDir).Any())
                        return current;

                    if (Directory.EnumerateFiles(categoriesDir, "_category.json", SearchOption.AllDirectories).Any())
                        return current;
                }
                catch
                {
                    // ignore and keep searching up
                }
            }

            var categoriesZip = Path.Combine(current, "Categories.zip");
            if (File.Exists(categoriesZip))
                return current;

            var parent = Directory.GetParent(current);
            if (parent is null) break;
            current = parent.FullName;
        }

        return AppContext.BaseDirectory;
    }

    private void Reload()
    {
        try
        {
            _errors.Info("Reload begin");
            // Load from disk first.
            var loaded = _schema.Load();
            _errors.Info($"Schema loaded: {loaded.Count} categories");

            // Apply to UI-bound collections on the UI thread.
            _dispatcher.Invoke(() =>
            {
                BulkUpdate(() =>
                {
                    // Detach old event hooks before we clear and rebuild.
                    UnwireHooks();

                    Categories.Clear();
                    foreach (var c in loaded)
                        Categories.Add(c);

                    WireHooks();

                    // Normalize Order to current list index (keeps drag ordering stable)
                    for (var i = 0; i < Categories.Count; i++)
                        Categories[i].Order = i;

                    SelectedCategory = Categories.FirstOrDefault();
                    ActiveCategory = SelectedCategory;
                    // Default behavior: categories start in "use all subcategories" mode, but we do NOT override per-subcategory file mode.
                    foreach (var c in Categories)
                    {
                        c.UseAllSubCategories = true;
                        c.SelectedSubCategory = c.SubCategories.FirstOrDefault();
                    }
// Previews are informational. Populate only for expanded items.
                    foreach (var s in Categories.SelectMany(c => c.SubCategories))
                    {
                        if (s.IsExpanded)
                            RefreshSubCategoryPreview(s);
                    }

                    SelectedSubCategory = Categories.SelectMany(c => c.SubCategories).FirstOrDefault();
                });
            });

            QueueRecomputePrompt(immediate: true);

            _errors.Info("Reload end");
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "Reload");
            SystemMessages = _errors.LatestMessage;
            _uiDialog.ShowError(ex.Message, "Reload failed");
        }
    }

    private void WireHooks()
    {
        foreach (var cat in Categories)
        {
            cat.PropertyChanged += OnCategoryPropertyChanged;
            foreach (var sub in cat.SubCategories)
                {
                    sub.PropertyChanged += OnSubCategoryPropertyChanged;
                    foreach (var entry in sub.Entries)
                        entry.PropertyChanged += OnEntryPropertyChanged;
                }
        }
    }

    private void UnwireHooks()
    {
        foreach (var cat in Categories)
        {
            cat.PropertyChanged -= OnCategoryPropertyChanged;
            foreach (var sub in cat.SubCategories)
                {
                    sub.PropertyChanged -= OnSubCategoryPropertyChanged;
                    foreach (var entry in sub.Entries)
                        entry.PropertyChanged -= OnEntryPropertyChanged;
                }
        }
    }

    private void OnCategoryPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not CategoryModel cat) return;
        if (IsBulkUpdating()) return;

        // User interaction telemetry (kept lightweight but specific).
        // We log the most meaningful properties that reflect user intent.
        var p = args.PropertyName ?? "";
        if (p == nameof(CategoryModel.Enabled))
            _errors.UiEvent("Category.Toggle", new { category = cat.Name, enabled = cat.Enabled });
        else if (p == nameof(CategoryModel.UseAllSubCategories))
            _errors.UiEvent("Category.UseAllSubCategories", new { category = cat.Name, value = cat.UseAllSubCategories });
        else if (p == nameof(CategoryModel.SelectedSubCategory))
            _errors.UiEvent("Category.SelectSubCategory", new { category = cat.Name, sub = cat.SelectedSubCategory?.Name });
        else if (p == nameof(CategoryModel.Order))
            _errors.UiEvent("Category.Reorder", new { category = cat.Name, order = cat.Order });
        else if (p == nameof(CategoryModel.Prefix) || p == nameof(CategoryModel.Suffix))
            _errors.UiEvent("Category.TextEdit", new { category = cat.Name, field = p, len = (p == nameof(CategoryModel.Prefix) ? cat.Prefix?.Length : cat.Suffix?.Length) });

        // Keep the global SelectedSubCategory aligned with per-category selection.
        if (args.PropertyName == nameof(CategoryModel.SelectedSubCategory) && cat.SelectedSubCategory is not null)
        {
            if (!ReferenceEquals(SelectedSubCategory, cat.SelectedSubCategory))
                SelectedSubCategory = cat.SelectedSubCategory;
        }

        QueueRecomputePrompt();
    }

    
    private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not PromptEntryModel entry) return;
        if (IsBulkUpdating()) return;
        if (_refreshingPreview) return;

        var p = args.PropertyName ?? "";
        if (p == nameof(PromptEntryModel.Enabled))
            _errors.UiEvent("Entry.Toggle", new { file = entry.Name, enabled = entry.Enabled });
        else if (p == nameof(PromptEntryModel.Order))
            _errors.UiEvent("Entry.Reorder", new { file = entry.Name, order = entry.Order });

        // Refresh previews if the changed entry belongs to the selected subcategory.
        var parentSub = Categories.SelectMany(c => c.SubCategories).FirstOrDefault(s => s.Entries.Contains(entry));
        if (parentSub is not null && ReferenceEquals(parentSub, SelectedSubCategory))
            RefreshSelectedPreview();

        QueueRecomputePrompt();
    }

private void OnSubCategoryPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is not SubCategoryModel sub) return;
        if (IsBulkUpdating()) return;

        // If we're currently refreshing previews, ignore property-changed callbacks that were
        // caused by that refresh (EntrySummary changes in particular).
        if (_refreshingPreview) return;

        var p = args.PropertyName ?? "";

        // User interaction telemetry (avoid ultra-chatty fields).
        if (p == nameof(SubCategoryModel.Enabled))
            _errors.UiEvent("SubCategory.Toggle", new { sub = sub.Name, enabled = sub.Enabled });
        else if (p == nameof(SubCategoryModel.SelectedTxtFile))
            _errors.UiEvent("SubCategory.SelectTxt", new { sub = sub.Name, file = sub.SelectedTxtFile });
        else if (p == nameof(SubCategoryModel.UseAllTxtFiles))
            _errors.UiEvent("SubCategory.UseAllTxtFiles", new { sub = sub.Name, value = sub.UseAllTxtFiles });
        else if (p == nameof(SubCategoryModel.Order))
            _errors.UiEvent("SubCategory.Reorder", new { sub = sub.Name, order = sub.Order });
        else if (p == nameof(SubCategoryModel.Prefix) || p == nameof(SubCategoryModel.Suffix))
            _errors.UiEvent("SubCategory.TextEdit", new { sub = sub.Name, field = p, len = (p == nameof(SubCategoryModel.Prefix) ? sub.Prefix?.Length : sub.Suffix?.Length) });

        // Some SubCategory properties are UI-only or output-only.
        // They must not trigger another prompt recompute (otherwise we can recurse endlessly).
        var prop = p;

        // UI-only signals should not trigger preview refresh or prompt recompute.
        if (prop == nameof(SubCategoryModel.IsDropTarget) || prop == nameof(SubCategoryModel.EntrySummary))
            return;

        // Keep per-subcategory preview in sync when expanded.
        if (prop == nameof(SubCategoryModel.IsExpanded) && sub.IsExpanded)
            RefreshSubCategoryPreview(sub);

        // CurrentEntry is set during prompt generation. Treat it as output-only.
        // Do NOT refresh previews or trigger prompt recompute from it, or we can end up
        // in a feedback loop (Generate -> CurrentEntry -> PropertyChanged -> Recompute -> ...).
        if (prop == nameof(SubCategoryModel.CurrentEntry))
        {
            if (sub.IsExpanded)
            {
                var cur = string.IsNullOrWhiteSpace(sub.CurrentEntry) ? "" : $"\nCurrent entry: {sub.CurrentEntry}";
                if (!string.IsNullOrWhiteSpace(sub.EntrySummary) && !sub.EntrySummary.EndsWith(cur))
                    sub.EntrySummary = sub.EntrySummary.Split("\nCurrent entry:")[0] + cur;
            }
            return;
        }

        // Only refresh the selected preview for meaningful input changes.
        if (ReferenceEquals(sub, SelectedSubCategory))
            RefreshSelectedPreview();

        QueueRecomputePrompt();
    }

    private void Save()
    {
        try
        {
            _schema.Save(Categories);
            SystemMessages = "Saved config to _category.json and _subcategory.json.";
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "Save");
            SystemMessages = _errors.LatestMessage;
            _uiDialog.ShowError(ex.Message, "Save failed");
        }
    }

    private bool IsBulkUpdating() => _bulkUpdateDepth > 0;

    private void BulkUpdate(Action action)
    {
        try
        {
            _bulkUpdateDepth++;
            action();
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "BulkUpdate");
            SystemMessages = _errors.LatestMessage;
        }
        finally
        {
            _bulkUpdateDepth = Math.Max(0, _bulkUpdateDepth - 1);
        }

        // One rebuild at the end.
        QueueRecomputePrompt();
    }

    internal void QueueRecomputePrompt(bool immediate = false)
    {
        QueueAutoSave();
        if (IsBulkUpdating()) return;

        _recomputeCts?.Cancel();
        _recomputeCts?.Dispose();
        _recomputeCts = new CancellationTokenSource();
        var token = _recomputeCts.Token;

        async void Run()
        {
            try
            {
                if (!immediate)
                    await Task.Delay(_recomputeDebounce, token);

                if (token.IsCancellationRequested) return;

                // Ensure prompt recompute happens on the UI thread.
                _dispatcher.Invoke(() => RecomputePrompt());
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                _errors.Report(ex, "QueueRecomputePrompt");
                SystemMessages = _errors.LatestMessage;
            }
        }

        Run();
    }

    private void AutoSave()
    {
        QueueAutoSave();
    }

    private void QueueAutoSave()
    {
        if (IsBulkUpdating()) return;

        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _autoSaveCts = new CancellationTokenSource();
        var token = _autoSaveCts.Token;

        // Snapshot categories on the UI thread to avoid collection mutation issues.
        var snapshot = Categories.ToList();

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_autoSaveDebounce, token);
                if (token.IsCancellationRequested) return;

                _schema.Save(snapshot);
                SaveUserSettings();

                _dispatcher.Invoke(() =>
                {
                    SystemMessages = "Auto-saved.";
                });
            }
            catch (OperationCanceledException)
            {
                // Ignore (debounce cancellation)
            }
            catch (Exception ex)
            {
                _errors.Report(ex, "AutoSave");
            }
        }, token);
    }

    private string UserSettingsPath => Path.Combine(AppDataStore.RootDir, "user_settings.json");

    private sealed class UserSettings
    {
        public string SwarmUrl { get; set; } = "http://127.0.0.1:7801";
        public string SwarmToken { get; set; } = "";

        public bool SendSwarmModelOverride { get; set; } = true;
        public string? SwarmSelectedModel { get; set; }

        public bool SendSwarmSteps { get; set; }
        public int SwarmSteps { get; set; }

        public bool SendSwarmCfgScale { get; set; }
        public double SwarmCfgScale { get; set; }

        public bool SendSwarmLoras { get; set; }
        public string? SwarmSelectedLora1 { get; set; }
        public double SwarmLora1Weight { get; set; } = 1.0;
        public string? SwarmSelectedLora2 { get; set; }
        public double SwarmLora2Weight { get; set; } = 1.0;

        public int BatchQty { get; set; } = 1;
        public bool BatchRandomizePrompts { get; set; }
    }

    private void LoadUserSettings()
    {
        try
        {
            if (!File.Exists(UserSettingsPath))
                return;

            var s = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(UserSettingsPath)) ?? new UserSettings();

            SwarmUrl = s.SwarmUrl;
            SwarmToken = s.SwarmToken;

            SendSwarmSteps = s.SendSwarmSteps;
            SwarmSteps = s.SwarmSteps;

            SendSwarmCfgScale = s.SendSwarmCfgScale;
            SwarmCfgScale = s.SwarmCfgScale;

            SendSwarmModelOverride = s.SendSwarmModelOverride;
            SwarmSelectedModel = s.SwarmSelectedModel ?? "";

            SendSwarmLoras = s.SendSwarmLoras;
            SwarmSelectedLora1 = s.SwarmSelectedLora1 ?? "";
            SwarmLora1Weight = s.SwarmLora1Weight;
            SwarmSelectedLora2 = s.SwarmSelectedLora2 ?? "";
            SwarmLora2Weight = s.SwarmLora2Weight;

            BatchQty = Math.Max(1, s.BatchQty);
            BatchRandomizePrompts = s.BatchRandomizePrompts;
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "LoadUserSettings");
        }
    }

    private void SaveUserSettings()
    {
        try
        {
            Directory.CreateDirectory(AppDataStore.RootDir);

            var s = new UserSettings
            {
                SwarmUrl = SwarmUrl,
                SwarmToken = SwarmToken,

                SendSwarmSteps = SendSwarmSteps,
                SwarmSteps = SwarmSteps,

                SendSwarmCfgScale = SendSwarmCfgScale,
                SwarmCfgScale = SwarmCfgScale,

                SendSwarmModelOverride = SendSwarmModelOverride,
                SwarmSelectedModel = SwarmSelectedModel,

                SendSwarmLoras = SendSwarmLoras,
                SwarmSelectedLora1 = SwarmSelectedLora1,
                SwarmLora1Weight = SwarmLora1Weight,
                SwarmSelectedLora2 = SwarmSelectedLora2,
                SwarmLora2Weight = SwarmLora2Weight,

                BatchQty = BatchQty,
                BatchRandomizePrompts = BatchRandomizePrompts
            };

            File.WriteAllText(UserSettingsPath, JsonSerializer.Serialize(s, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "SaveUserSettings");
        }
    }


    private static long? NormalizeSeedForSwarmUi(string? seedText)
    {
        if (string.IsNullOrWhiteSpace(seedText)) return null;

        if (!long.TryParse(seedText.Trim(), out var seed)) return null;

        // SwarmUI accepts -1 for random, or any non-negative integer seed.
        // Any other negative number causes an API error.
        if (seed < -1) return -1;
        return seed;
    }

    private void RecomputePrompt(int? seedOverride = null)
    {
        try
        {
            // NOTE (2025-12-22): SwarmUI accepts seed values of -1 (random) or any non-negative number.
            // Other negative numbers cause SwarmUI to error, so we clamp them to -1.
            var seedLong = seedOverride.HasValue ? seedOverride.Value : NormalizeSeedForSwarmUi(SeedText);
            int? seed = seedLong is null
                ? null
                : (seedLong.Value <= int.MaxValue ? (int)seedLong.Value : int.MaxValue);

            var result = _engine.Generate(Categories, seed);
            PromptText = result.Prompt;
            SystemMessages = string.Join(Environment.NewLine, result.Messages);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "RecomputePrompt");
            SystemMessages = _errors.LatestMessage;
        }
    }

    private void Copy()
    {
        if (string.IsNullOrWhiteSpace(PromptText)) return;

        _clipboard.SetText(PromptText);
        ShowCopiedOnButton();
    }

    private void ShowCopiedOnButton()
    {
        try
        {
            CopyButtonText = "Copied!";

            _copyResetTimer?.Stop();
            _copyResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
            _copyResetTimer.Tick += (_, __) =>
            {
                _copyResetTimer?.Stop();
                CopyButtonText = "Copy";
            };
            _copyResetTimer.Start();
        }
        catch
        {
            // best effort
            CopyButtonText = "Copy";
        }
    }

    private void SendToSwarmUi()
    {
        if (string.IsNullOrWhiteSpace(PromptText)) return;
        var seed = NormalizeSeedForSwarmUi(SeedText);
        var batch = CreateBatchForRun(title: "Single", promptSnapshot: PromptText, seed: seed);
        _dispatcher.Invoke(() => AddRecentBatch(batch));
        _ = StartSwarmGenerationAsync(batch, PromptText, seed);
    }

    private void SendBatchToSwarmUi()
    {
        // Fire off multiple generations grouped into a single batch.
        // "Randomized prompts" here means: regenerate the PromptLoom prompt using a fresh seed each time.
        var batch = CreateBatchForRun(title: $"Batch ×{BatchQty}", promptSnapshot: PromptText, seed: null);
        _dispatcher.Invoke(() => AddRecentBatch(batch));

        for (var i = 0; i < BatchQty; i++)
        {
            string prompt;
            long? seed;

            if (BatchRandomizePrompts)
            {
                var s = _random.Next(0, int.MaxValue);
                seed = s;
                try
                {
                    var res = _engine.Generate(Categories, s);
                    prompt = res.Prompt;
                }
                catch
                {
                    // Fallback: reuse current prompt.
                    prompt = PromptText;
                }
            }
            else
            {
                prompt = PromptText;
                seed = NormalizeSeedForSwarmUi(SeedText);
            }

            if (string.IsNullOrWhiteSpace(prompt)) continue;
            _ = StartSwarmGenerationAsync(batch, prompt, seed);
        }
    }

    private string ApplySelectedLorasToPrompt(string prompt)
    {
        if (!SendSwarmLoras) return prompt;

        var sb = new StringBuilder(prompt);

        void Add(string name, double weight)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            // SwarmUI prompt syntax: <lora:filename:weight>
            sb.Append(' ');
            sb.Append("<lora:");
            sb.Append(name);
            sb.Append(':');
            sb.Append(weight.ToString("0.###", CultureInfo.InvariantCulture));
            sb.Append('>');
        }

        Add(SwarmSelectedLora1, SwarmLora1Weight);
        Add(SwarmSelectedLora2, SwarmLora2Weight);

        return sb.ToString();
    }

    private string BuildLorasSummary()
    {
        if (!SendSwarmLoras) return "";
        var parts = new List<string>();
        void Add(string name, double weight)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            parts.Add($"{name} ({weight:0.###})");
        }
        Add(SwarmSelectedLora1, SwarmLora1Weight);
        Add(SwarmSelectedLora2, SwarmLora2Weight);
        return string.Join(", ", parts);
    }

    private RecentSwarmBatchViewModel CreateBatchForRun(string title, string promptSnapshot, long? seed)
    {
        // Capture a snapshot of the settings at the moment the batch is created.
        var effectiveModel = (SendSwarmModelOverride && !string.IsNullOrWhiteSpace(SwarmSelectedModel)) ? SwarmSelectedModel : "(auto)";
        var steps = SendSwarmSteps ? SwarmSteps.ToString() : "(auto)";
        var cfg = SendSwarmCfgScale ? SwarmCfgScale.ToString("0.###", CultureInfo.InvariantCulture) : "(auto)";
        var loras = BuildLorasSummary();
        var seedText = seed is null ? "(varies)" : seed.Value.ToString();

        var settings = $"Model: {effectiveModel}   Steps: {steps}   CFG: {cfg}   Seed: {seedText}";
        if (!string.IsNullOrWhiteSpace(loras)) settings += $"   LoRAs: {loras}";

        return new RecentSwarmBatchViewModel(title, promptSnapshot ?? string.Empty, settings, _clock);
    }

    private async Task StartSwarmGenerationAsync(RecentSwarmBatchViewModel batch, string promptSnapshot, long? seed)
    {
        RecentSwarmImageViewModel? item = null;
        CancellationTokenSource? cts = null;

        try
        {
            if (!Uri.TryCreate(SwarmUrl, UriKind.Absolute, out var baseUri))
                throw new InvalidOperationException($"Invalid SwarmUrl: '{SwarmUrl}'. Expected something like http://127.0.0.1:7801");

            cts = new CancellationTokenSource();

            var lorasSummary = BuildLorasSummary();
            item = new RecentSwarmImageViewModel(
                promptSnapshot,
                onCancel: () =>
            {
                try { cts.Cancel(); } catch { }
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var cancelClient = BuildSwarmClient(baseUri);
                        await cancelClient.InterruptAllAsync(otherSessions: false, ct: CancellationToken.None).ConfigureAwait(false);
                    }
                    catch { /* best effort */ }
                });
                _dispatcher.Invoke(() =>
                {
                    if (item is null) return;
                    item.IsGenerating = false;
                    item.IsCancelled = true;
                    item.IsIndeterminate = false;
                    item.Status = "Cancelled.";
                });
                },
                model: null,
                seed: seed,
                steps: SendSwarmSteps ? SwarmSteps : null,
                cfgScale: SendSwarmCfgScale ? SwarmCfgScale : null,
                lorasSummary: lorasSummary);

            // Initial placeholder thumbnail generated from prompt text.
            item.Thumbnail = ThumbnailRenderer.RenderPromptThumbnail(promptSnapshot);
            _dispatcher.Invoke(() => batch.Items.Add(item));

            var client = BuildSwarmClient(baseUri);
            var (suggestedModel, w, h) = await client.GetSuggestedModelAndResolutionAsync(cts.Token).ConfigureAwait(false);

            // Model override is optional, but the API still needs *a* model.
            var modelToUse = suggestedModel;
            if (SendSwarmModelOverride && !string.IsNullOrWhiteSpace(SwarmSelectedModel))
                modelToUse = SwarmSelectedModel;

            // Stamp effective settings on the item now that we know the resolved model.
            item.Model = string.IsNullOrWhiteSpace(modelToUse) ? "OfficialStableDiffusion/sd_xl_base_1.0" : modelToUse;
            item.Steps = SendSwarmSteps ? SwarmSteps : null;
            item.CfgScale = SendSwarmCfgScale ? SwarmCfgScale : null;
            item.LorasSummary = lorasSummary;

            var finalPrompt = ApplySelectedLorasToPrompt(promptSnapshot);

            var req = new GenerateText2ImageRequest
            {
                Prompt = finalPrompt,
                NegativePrompt = "",
                Model = string.IsNullOrWhiteSpace(modelToUse) ? "OfficialStableDiffusion/sd_xl_base_1.0" : modelToUse,
                Width = w,
                Height = h,
                Seed = seed,
                Steps = SendSwarmSteps ? SwarmSteps : null,
                CfgScale = SendSwarmCfgScale ? SwarmCfgScale : null,
                Extra = new Dictionary<string, object?>
                {
                    ["images"] = 1,
                }
            };

            await foreach (var frame in client.GenerateText2ImageStreamAsync(req, cts.Token).ConfigureAwait(false))
            {
                if (cts.IsCancellationRequested) break;

                if (SwarmWsParser.TryParseSwarmWsFrame(frame, out var status, out var progress01, out var previewDataUrl, out var finalImageRef, out var seedUsed))
                {
                    _dispatcher.Invoke(() =>
                    {
                        if (item is null) return;

                        if (!string.IsNullOrWhiteSpace(status))
                            item.Status = status;

                        if (seedUsed is not null)
                        {
                            // Keep the Prompt tab's seed display in sync with the actual SwarmUI seed.
                            SetSeedTextFromSwarm(seedUsed.Value);
                            item.Seed = seedUsed.Value;
                        }

                        if (progress01 is not null)
                        {
                            item.IsIndeterminate = false;
                            item.Progress = Math.Clamp(progress01.Value, 0, 1);
                        }

                        if (!string.IsNullOrWhiteSpace(previewDataUrl))
                        {
                            var img = SwarmImageDecoder.TryDecodeToImageSource(previewDataUrl);
                            if (img is not null)
                                item.Thumbnail = img;
                        }
                    });

                    if (!string.IsNullOrWhiteSpace(finalImageRef))
                    {
                        var final = await TryDownloadSwarmImageAsync(baseUri, finalImageRef!, cts.Token).ConfigureAwait(false);
                        if (final is not null)
                        {
                            _dispatcher.Invoke(() =>
                            {
                                if (item is null) return;
                                item.Thumbnail = final;
                            });
                        }
                    }
                }
            }

            _dispatcher.Invoke(() =>
            {
                if (item is null) return;
                if (!item.IsCancelled)
                {
                    item.IsGenerating = false;
                    item.IsComplete = true;
                    item.IsIndeterminate = false;
                    item.Progress = item.Progress <= 0 ? 1 : item.Progress;
                    item.Status = item.IsFailed ? "Failed." : "Done.";
                }
            });

            SystemMessages = "Sent to SwarmUI (streaming). Latest Images shows progress and previews.";
        }
        catch (OperationCanceledException)
        {
            // expected
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "StartSwarmGenerationAsync");

            _dispatcher.Invoke(() =>
            {
                if (item is null) return;
                item.IsGenerating = false;
                item.IsIndeterminate = false;
                item.IsFailed = true;
                item.Status = "Failed.";
            });

            SystemMessages = _errors.LatestMessage;
            _uiDialog.ShowError(ex.Message, "SwarmUI send failed");
        }
        finally
        {
            IsSwarmGenerating = false;
        }
    }

    private void ShowImageOverlay(object? param)
    {
        if (param is RecentSwarmImageViewModel vm)
        {
            OverlaySelectedItem = vm;
            OverlayImage = vm.Thumbnail;
            OverlayDetailsExpanded = false;
            IsImageOverlayVisible = true;
            return;
        }
        if (param is ImageSource img)
        {
            OverlaySelectedItem = null;
            OverlayImage = img;
            OverlayDetailsExpanded = false;
            IsImageOverlayVisible = true;
        }
    }

    private void HideImageOverlay()
    {
        IsImageOverlayVisible = false;
        OverlayImage = null;
        OverlaySelectedItem = null;
        OverlayDetailsExpanded = false;
    }

    private void NavigateOverlay(int dir)
    {
        try
        {
            if (OverlaySelectedItem is null)
                return;

            // Display order: newest batch first (RecentBatches[0] is newest), request order within each batch.
            var flat = new List<RecentSwarmImageViewModel>(64);
            foreach (var b in RecentBatches)
            {
                foreach (var it in b.Items)
                {
                    // Only completed generations are navigable in the overlay.
                    if (it.IsComplete)
                        flat.Add(it);
                }
            }

            if (flat.Count == 0)
                return;

            var idx = flat.IndexOf(OverlaySelectedItem);
            if (idx < 0)
                idx = 0;

            var next = (idx + dir) % flat.Count;
            if (next < 0) next += flat.Count;

            var vm = flat[next];
            OverlaySelectedItem = vm;
            OverlayImage = vm.Thumbnail;
            OverlayDetailsExpanded = false;
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "NavigateOverlay");
        }
    }

    private SwarmUiClient BuildSwarmClient(Uri baseUri)
    {
        return new SwarmUiClient(new SwarmUiClientOptions
        {
            BaseUrl = baseUri,
            AutoSession = true,
            SwarmToken = string.IsNullOrWhiteSpace(SwarmToken) ? null : SwarmToken,
            // Timeout is controlled via CancellationToken + user cancellation UI.
            Timeout = System.Threading.Timeout.InfiniteTimeSpan
        });
    }

    private void AddRecentBatch(RecentSwarmBatchViewModel batch)
    {
        // Keep a small rolling history for the Prompt tab history.
        // Most recent first.
        RecentBatches.Insert(0, batch);
        while (RecentBatches.Count > 12)
            RecentBatches.RemoveAt(RecentBatches.Count - 1);
    }

private static async Task<ImageSource?> TryDownloadSwarmImageAsync(Uri baseUri, string imageRef, CancellationToken ct)
    {
        try
        {
            // Swarm builds sometimes return a relative path (e.g. /View/...) or a full URL.
            if (!Uri.TryCreate(imageRef, UriKind.Absolute, out var imageUri))
            {
                // Normalize to absolute.
                var rel = imageRef.StartsWith("/") ? imageRef : "/" + imageRef;
                imageUri = new Uri(baseUri, rel);
            }

            var bytes = await s_http.GetByteArrayAsync(imageUri, ct);
            using var ms = new MemoryStream(bytes);
            var bmp = new BitmapImage();
            bmp.BeginInit();
            bmp.CacheOption = BitmapCacheOption.OnLoad;
            bmp.StreamSource = ms;
            bmp.EndInit();
            bmp.Freeze();
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    private void ShowSentOnSwarmButton()
    {
        try
        {
            SwarmButtonText = "Sent!";

            _swarmResetTimer?.Stop();
            _swarmResetTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
            _swarmResetTimer.Tick += (_, __) =>
            {
                _swarmResetTimer?.Stop();
                SwarmButtonText = "Send Prompt to SwarmUI";
            };
            _swarmResetTimer.Start();
        }
        catch
        {
            SwarmButtonText = "Send Prompt to SwarmUI";
        }
    }

    private void Randomize()
    {
        // Generate a new random seed and trigger prompt recomputation. Setting the seed
        // property will cause RecomputePrompt() via its setter.
        // NOTE (2025-12-22): SwarmUI rejects negative seeds other than -1.
        // Keep randomized seeds non-negative so they are always valid for both PromptLoom and SwarmUI.
        SeedText = _random.Next(0, int.MaxValue).ToString();
        // When prefixes/suffixes are changed after randomization, recompute prompt using new random seed.
        QueueRecomputePrompt(immediate: true);
    }

    

    private async void TestSwarmConnection()
    {
        try
        {
            if (!Uri.TryCreate(SwarmUrl, UriKind.Absolute, out var baseUri))
                throw new InvalidOperationException($"Invalid SwarmUrl: '{SwarmUrl}'.");

            var client = BuildSwarmClient(baseUri);
            var status = await client.GetCurrentStatusAsync(CancellationToken.None).ConfigureAwait(false);

	            _dispatcher.Invoke(() =>
            {
	                // FIX: build error CS1039 "Unterminated string literal" | CAUSE: multiline interpolated string split across lines
	                // CHANGE: use explicit newline escape to keep the string on one source line
	                // DATE: 2025-12-22
	                SwarmUiStatusText = $"Connected. Status: {status.Status}\n{status.Detail}";
	            });
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "TestSwarmConnection");
            SwarmUiStatusText = "Connection failed: " + ex.Message;
        }
    }

    private async void RefreshSwarmModels()
    {
        try
        {
            if (!Uri.TryCreate(SwarmUrl, UriKind.Absolute, out var baseUri))
                throw new InvalidOperationException($"Invalid SwarmUrl: '{SwarmUrl}'.");

            SwarmUiStatusText = "Loading models...";

            var client = BuildSwarmClient(baseUri);
            var models = await client.ListStableDiffusionModelsAsync(depth: 8, ct: CancellationToken.None).ConfigureAwait(false);

            _dispatcher.Invoke(() =>
            {
                SwarmModels.Clear();
                foreach (var m in models)
                    SwarmModels.Add(m);

                if (!string.IsNullOrWhiteSpace(SwarmSelectedModel) && SwarmModels.Contains(SwarmSelectedModel))
                {
                    // keep user's selection
                }
                else if (SwarmModels.Count > 0)
                {
                    SwarmSelectedModel = SwarmModels[0];
                }

                SwarmUiStatusText = models.Count == 0
                    ? "Connected, but no models were returned (permissions or backend still loading?)."
                    : $"Loaded {models.Count} models.";
            });
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "RefreshSwarmModels");
            SwarmUiStatusText = "Failed to load models: " + ex.Message;
        }
    }

    private async void RefreshSwarmLoras()
    {
        try
        {
            if (!Uri.TryCreate(SwarmUrl, UriKind.Absolute, out var baseUri))
                throw new InvalidOperationException($"Invalid SwarmUrl: '{SwarmUrl}'.");

            SwarmUiStatusText = "Loading LoRAs...";

            var client = BuildSwarmClient(baseUri);
            var loras = await client.ListLorasAsync(depth: 8, ct: CancellationToken.None).ConfigureAwait(false);

            _dispatcher.Invoke(() =>
            {
                SwarmLoras.Clear();
                foreach (var l in loras)
                    SwarmLoras.Add(l);

                SwarmUiStatusText = loras.Count == 0
                    ? "Connected, but no LoRAs were returned (none installed or backend still loading?)."
                    : $"Loaded {loras.Count} LoRAs.";
            });
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "RefreshSwarmLoras");
            SwarmUiStatusText = "Failed to load LoRAs: " + ex.Message;
        }
    }

    private void OpenSwarmUi()
    {
        try
        {
            _process.Start(new ProcessStartInfo { FileName = SwarmUrl, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "OpenSwarmUi");
            SwarmUiStatusText = "Failed to open browser: " + ex.Message;
        }
    }

private void SaveOutput()
    {
        if (string.IsNullOrWhiteSpace(PromptText)) return;

        try
        {
            Directory.CreateDirectory(_schema.OutputDir);
            var ts = DateTime.Now;
            var baseName = $"prompt_{ts:yyyyMMdd_HHmmss}";
            var path = Path.Combine(_schema.OutputDir, baseName + ".txt");
            File.WriteAllText(path, PromptText);
            SystemMessages = $"Saved prompt to {path}";
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "SaveOutput");
            SystemMessages = _errors.LatestMessage;
            _uiDialog.ShowError(ex.Message, "Save prompt failed");
        }
    }

    private void OpenFolder()
    {
        try
        {
            _schema.EnsureBaseFolders();
            _process.Start(new ProcessStartInfo
            {
                FileName = _schema.CategoriesDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "OpenFolder");
            SystemMessages = _errors.LatestMessage;
            _uiDialog.ShowError(ex.Message, "Open folder failed");
        }
    }

    private void RestoreOriginalCategories()
    {
        try
        {
            // This is an explicit, user-initiated action. We always back up the current Categories first.
            AppDataStore.RestoreBundledCategories(_installDir, _errors);

            // Reload view-model state from disk.
            Reload();

            SystemMessages = "Restored original Categories (backup created in Output folder).";
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "RestoreOriginalCategories");
            SystemMessages = _errors.LatestMessage;
            _uiDialog.ShowError(ex.Message, "Restore Categories failed");
        }
    }

    
    private void SelectEntry(object? param)
    {
        if (param is PromptEntryModel entry)
        {
            // Ensure parent subcategory is selected for editing context *before* setting SelectedEntry
            // so that the SelectedEntry setter can mirror the selection onto SubCategoryModel.SelectedEntry.
            var parentSub = Categories.SelectMany(c => c.SubCategories).FirstOrDefault(s => s.Entries.Contains(entry));
            if (parentSub is not null && !ReferenceEquals(SelectedSubCategory, parentSub))
                SelectedSubCategory = parentSub;

            SelectedEntry = entry;
        }
    }

    private void MoveEntryUp(object? param)
    {
        if (param is not PromptEntryModel entry) return;
        var sub = Categories.SelectMany(c => c.SubCategories).FirstOrDefault(s => s.Entries.Contains(entry));
        if (sub is null) return;

        var idx = sub.Entries.IndexOf(entry);
        if (idx <= 0) return;

        sub.Entries.Move(idx, idx - 1);
        NormalizeEntryOrders(sub);
        QueueRecomputePrompt();
    }

    private void MoveEntryDown(object? param)
    {
        if (param is not PromptEntryModel entry) return;
        var sub = Categories.SelectMany(c => c.SubCategories).FirstOrDefault(s => s.Entries.Contains(entry));
        if (sub is null) return;

        var idx = sub.Entries.IndexOf(entry);
        if (idx < 0 || idx >= sub.Entries.Count - 1) return;

        sub.Entries.Move(idx, idx + 1);
        NormalizeEntryOrders(sub);
        QueueRecomputePrompt();
    }

    private static void NormalizeEntryOrders(SubCategoryModel sub)
    {
        for (var i = 0; i < sub.Entries.Count; i++)
            sub.Entries[i].Order = i;
    }

    private void LoadSelectedEntryFile()
    {
        if (SelectedEntry is null)
        {
            EntryEditorPath = "";
            EntryEditorText = "";
            return;
        }

        try
        {
            EntryEditorPath = SelectedEntry.FilePath;
            EntryEditorText = File.Exists(SelectedEntry.FilePath) ? File.ReadAllText(SelectedEntry.FilePath) : "";
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "LoadSelectedEntryFile");
            EntryEditorText = "(unable to read file)";
        }
    }

    private void SaveEntryFile()
    {
        if (SelectedEntry is null) return;
        try
        {
            File.WriteAllText(SelectedEntry.FilePath, EntryEditorText ?? "");
            _errors.Info("Saved entry file: " + SelectedEntry.FilePath);
            RefreshSelectedPreview();

            // Keep file selection in sync: when subcategory changes, default to its selected/first file.
            if (SelectedSubCategory is not null)
                SelectedEntry = SelectedSubCategory.SelectedEntry ?? SelectedSubCategory.Entries.FirstOrDefault();

            QueueRecomputePrompt();
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "SaveEntryFile");
        }
    }

    private void OpenEntryFile()
    {
        if (SelectedEntry is null) return;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = SelectedEntry.FilePath,
                UseShellExecute = true
            };
            _process.Start(psi);
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "OpenEntryFile");
        }
    }

private void SelectSubCategory(object? param)
    {
        if (param is SubCategoryModel sub)
            SelectedSubCategory = sub;
    }

    public void MoveCategory(CategoryModel dragged, CategoryModel target)
    {
        var from = Categories.IndexOf(dragged);
        var to = Categories.IndexOf(target);
        if (from < 0 || to < 0 || from == to) return;

        Categories.Move(from, to);

        for (var i = 0; i < Categories.Count; i++)
            Categories[i].Order = i;

        QueueRecomputePrompt(immediate: true);
    }

    private void MoveCategoryUp(object? param)
    {
        if (param is not CategoryModel cat) return;
        var from = Categories.IndexOf(cat);
        if (from <= 0) return;
        Categories.Move(from, from - 1);
        for (var i = 0; i < Categories.Count; i++)
            Categories[i].Order = i;
        QueueRecomputePrompt(immediate: true);
    }

    private void MoveCategoryDown(object? param)
    {
        if (param is not CategoryModel cat) return;
        var from = Categories.IndexOf(cat);
        if (from < 0 || from >= Categories.Count - 1) return;
        Categories.Move(from, from + 1);
        for (var i = 0; i < Categories.Count; i++)
            Categories[i].Order = i;
        QueueRecomputePrompt(immediate: true);
    }

    public void MoveSubCategory(CategoryModel parent, SubCategoryModel dragged, SubCategoryModel target)
    {
        var from = parent.SubCategories.IndexOf(dragged);
        var to = parent.SubCategories.IndexOf(target);
        if (from < 0 || to < 0 || from == to) return;

        parent.SubCategories.Move(from, to);
        for (var i = 0; i < parent.SubCategories.Count; i++)
            parent.SubCategories[i].Order = i;

        QueueRecomputePrompt(immediate: true);
    }

    private void MoveSubCategoryUp(object? param)
    {
        if (param is not SubCategoryModel sub) return;
        var parent = Categories.FirstOrDefault(c => c.SubCategories.Contains(sub));
        if (parent is null) return;
        var idx = parent.SubCategories.IndexOf(sub);
        if (idx <= 0) return;
        parent.SubCategories.Move(idx, idx - 1);
        for (var i = 0; i < parent.SubCategories.Count; i++)
            parent.SubCategories[i].Order = i;
        QueueRecomputePrompt(immediate: true);
    }

    private void MoveSubCategoryDown(object? param)
    {
        if (param is not SubCategoryModel sub) return;
        var parent = Categories.FirstOrDefault(c => c.SubCategories.Contains(sub));
        if (parent is null) return;
        var idx = parent.SubCategories.IndexOf(sub);
        if (idx < 0 || idx >= parent.SubCategories.Count - 1) return;
        parent.SubCategories.Move(idx, idx + 1);
        for (var i = 0; i < parent.SubCategories.Count; i++)
            parent.SubCategories[i].Order = i;
        QueueRecomputePrompt(immediate: true);
    }

    private void SelectAllSubCategories(object? param)
    {
        if (param is not CategoryModel cat) return;
        BulkUpdate(() =>
        {
            // UX: "All" implies the category itself is enabled.
            cat.Enabled = true;
            foreach (var s in cat.SubCategories) s.Enabled = true;
        });
    }

    private void SelectNoneSubCategories(object? param)
    {
        if (param is not CategoryModel cat) return;
        BulkUpdate(() =>
        {
            foreach (var s in cat.SubCategories) s.Enabled = false;
        });
    }

private void SelectAllEntries(object? param)
{
    if (param is not SubCategoryModel sub) return;
    BulkUpdate(() =>
    {
        foreach (var e in sub.Entries) e.Enabled = true;
    });
}

private void SelectNoneEntries(object? param)
{
    if (param is not SubCategoryModel sub) return;
    BulkUpdate(() =>
    {
        foreach (var e in sub.Entries) e.Enabled = false;
    });
}


    private void RefreshSelectedPreview()
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(RefreshSelectedPreview);
            return;
        }

        if (_refreshingPreview) return;
        try
        {
            _refreshingPreview = true;

            SelectedSubCategoryPreview.Clear();

            if (SelectedSubCategory is null)
            {
                SelectedSubCategoryEntrySummary = "No subcategory selected.";
                return;
            }

            RefreshSubCategoryPreview(SelectedSubCategory);

            // Mirror the currently selected subcategory into legacy fields (no longer shown in 1.5.1 UI).
            SelectedSubCategoryEntrySummary = SelectedSubCategory.EntrySummary;
            foreach (var e in SelectedSubCategory.PreviewEntries.Take(120))
                SelectedSubCategoryPreview.Add(e);
        }
        finally
        {
            _refreshingPreview = false;
        }
    }

    private void RefreshSubCategoryPreview(SubCategoryModel sub)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.Invoke(() => RefreshSubCategoryPreview(sub));
            return;
        }

        sub.PreviewEntries.Clear();

        // Decide which files are currently active for preview.
        var enabled = sub.Entries
            .OrderBy(e => e.Order)
            .ThenBy(e => e.Name)
            .Where(e => e.Enabled)
            .ToList();

        List<string> entries;
        try
        {
            if (sub.UseAllTxtFiles)
            {
                var files = enabled.Count > 0 ? enabled : sub.Entries.ToList();
                entries = files.SelectMany(e => _fileReader.LoadWildcardFile(e.FilePath)).ToList();
            }
            else
            {
                var selected = sub.SelectedEntry ?? enabled.FirstOrDefault() ?? sub.Entries.FirstOrDefault();
                if (selected is null)
                {
                    sub.EntrySummary = "(no entry files)";
                    return;
                }
                entries = _fileReader.LoadWildcardFile(selected.FilePath);
            }
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "RefreshSubCategoryPreview:LoadEntries");
            sub.EntrySummary = "(unable to load entries)";
            return;
        }

        sub.EntrySummary = $"{entries.Count} entries loaded.";
        foreach (var e in entries.Take(200))
            sub.PreviewEntries.Add(e);// Keep the summary informative even before the first prompt generation.
        if (!string.IsNullOrWhiteSpace(sub.CurrentEntry))
            sub.EntrySummary = $"{entries.Count} entries loaded.\nCurrent entry: {sub.CurrentEntry}";
    }

    private string FindParentCategoryName(SubCategoryModel sub)
        => Categories.FirstOrDefault(c => c.SubCategories.Contains(sub))?.Name ?? "?";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
