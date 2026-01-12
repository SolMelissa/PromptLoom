// CHANGE LOG
// - 2026-03-09 | Fix: Cleanup | Remove unused JSON import after tag-only refactor.
// - 2026-03-06 | Request: Tag-only mode | Remove category workflows and generate prompts from tag-selected files.
// - 2026-03-06 | Request: Clear startup prompt | Gate prompt recompute until user interaction.

// NOTE (PromptLoom 1.8.0.1):
// SwarmUI integration: added a SwarmUI bridge reference and a "Send Prompt to SwarmUI" button/command.
// IMPORTANT: PromptLoom intentionally does NOT send model/resolution/steps/sampler so SwarmUI's current UI settings remain authoritative.
//
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
using PromptLoom.Services;
using SwarmUi.Client;
using XLemmatizer;

namespace PromptLoom.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly TagPromptBuilder _promptBuilder;
    private readonly IWildcardFileReader _fileReader;
    private readonly IClock _clock;
    private readonly IRandomSource _randomSource;
    private readonly Random _random;
    private readonly IUiDialogService _uiDialog;
    private readonly IClipboardService _clipboard;
    private readonly IProcessService _process;
    private readonly IDispatcherService _dispatcher;
    private readonly IFileSystem _fileSystem;
    private readonly IAppDataStore _appDataStore;
    private readonly IUserSettingsStore _settingsStore;
    private readonly IAppSwarmUiService _swarmService;

    private readonly IErrorReporter _errors;
    private bool _suppressPromptRecompute;
    private bool _promptEnabled;

    // Reuse a single HttpClient instance for image downloads.
    private static readonly HttpClient s_http = new HttpClient();

    public ObservableCollection<string> ErrorEntries => _errors.Entries;
    public SearchViewModel SearchViewModel { get; }

    // Debounce prompt recomputation to avoid rebuilding several times per UI gesture.
    private CancellationTokenSource? _recomputeCts;
    private readonly TimeSpan _recomputeDebounce = TimeSpan.FromMilliseconds(90);

    private CancellationTokenSource? _autoSaveCts;
    private readonly TimeSpan _autoSaveDebounce = TimeSpan.FromMilliseconds(800);

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
        set { _swarmSelectedModel = value; OnPropertyChanged(); QueueAutoSave(); }
    }

    public ObservableCollection<SwarmAssetCardViewModel> SwarmModels { get; } = new();

    public ObservableCollection<SwarmAssetCardViewModel> SwarmLoras { get; } = new();

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
        set { _sendSwarmCfgScale = value; OnPropertyChanged(); QueueAutoSave(); OnPropertyChanged(nameof(CanEditSwarmCfgScale)); }
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

    private bool _sendSwarmSeed = true;
    public bool SendSwarmSeed
    {
        get => _sendSwarmSeed;
        set { _sendSwarmSeed = value; OnPropertyChanged(); QueueAutoSave(); }
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
        set { _swarmSelectedLora2 = value; OnPropertyChanged(); QueueAutoSave(); }
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
        set { _batchQty = Math.Clamp(value, 2, 50); OnPropertyChanged(); QueueAutoSave(); }
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

    private string _promptText = "";
    public string PromptText { get => _promptText; set { _promptText = value; OnPropertyChanged(); } }

    private string _promptSeedText = "";
    // NOTE (2025-12-25): PromptSeedText is user-editable and triggers prompt recomputation.
    public string PromptSeedText
    {
        get => _promptSeedText;
        set
        {
            if (string.Equals(_promptSeedText, value, StringComparison.Ordinal))
                return;

            _promptSeedText = value;
            OnPropertyChanged();
            EnablePromptGeneration();
            RecomputePrompt();
        }
    }

    private string _imageSeedText = "";
    // NOTE (2025-12-31): ImageSeedText is used only for SwarmUI image generation.
    public string ImageSeedText
    {
        get => _imageSeedText;
        set { _imageSeedText = value; OnPropertyChanged(); }
    }

    private void SetImageSeedTextFromSwarm(long seed)
    {
        ImageSeedText = seed.ToString(CultureInfo.InvariantCulture);
    }

    // (UI) Copy feedback is shown directly on the Copy button.

    private string _systemMessages = "";
    public string SystemMessages { get => _systemMessages; set { _systemMessages = value; OnPropertyChanged(); } }

    public RelayCommand CopyCommand { get; }
    public RelayCommand SendToSwarmUiCommand { get; }
    public RelayCommand SendBatchToSwarmUiCommand { get; }
    public RelayCommand RandomizeCommand { get; }
    public AsyncRelayCommand TestSwarmConnectionCommand { get; }
    public AsyncRelayCommand RefreshSwarmModelsCommand { get; }
    public RelayCommand RefreshSwarmLorasCommand { get; }
    public RelayCommand ShowImageOverlayCommand { get; }
    public RelayCommand HideImageOverlayCommand { get; }
    public RelayCommand ToggleOverlayDetailsCommand { get; }
    public RelayCommand OverlayPrevCommand { get; }
    public RelayCommand OverlayNextCommand { get; }
    public RelayCommand OpenSwarmUiCommand { get; }
    public RelayCommand SaveOutputCommand { get; }
    public RelayCommand OpenFolderCommand { get; }

    /// <summary>
    /// Window-driven initialization entrypoint.
    /// We intentionally do not touch disk or mutate UI-bound collections inside the constructor,
    /// because bindings may not be fully established yet.
    /// </summary>
    public void Initialize()
    {
        _suppressPromptRecompute = true;
        PromptText = "";
        SystemMessages = "";
        _suppressPromptRecompute = false;
        _ = SearchViewModel.InitializeAsync();

        // Load Swarm server metadata on startup (best effort).
        // Fire-and-forget async methods which update observable collections on the UI thread.
        _ = RefreshSwarmModels();
        RefreshSwarmLoras();
    }

    /// <summary>
    /// Creates a new main view model with optional injected services.
    /// </summary>
    public MainViewModel(
        IFileSystem? fileSystem = null,
        IAppDataStore? appDataStore = null,
        IUserSettingsStore? settingsStore = null,
        ISwarmUiClientFactory? swarmClientFactory = null,
        IAppSwarmUiService? swarmService = null,
        IErrorReporter? errorReporter = null,
        IWildcardFileReader? fileReader = null,
        IClock? clock = null,
        IRandomSource? randomSource = null,
        IUiDialogService? uiDialog = null,
        IClipboardService? clipboard = null,
        IProcessService? process = null,
        IDispatcherService? dispatcher = null,
        SearchViewModel? searchViewModel = null)
    {
        _errors = errorReporter ?? new ErrorReporterAdapter();
        _errors.Info("MainViewModel ctor begin");

        _fileSystem = fileSystem ?? new FileSystem();
        _appDataStore = appDataStore ?? new AppDataStoreAdapter();
        _settingsStore = settingsStore ?? new UserSettingsStore(_fileSystem, _appDataStore);
        var resolvedSwarmFactory = swarmClientFactory ?? new SwarmUiClientFactory();
        _swarmService = swarmService ?? new SwarmUiService(resolvedSwarmFactory);
        _fileReader = fileReader ?? new WildcardFileReader();
        _clock = clock ?? new SystemClock();
        _randomSource = randomSource ?? new SystemRandomSource();
        _random = _randomSource.Create(null);
        _promptBuilder = new TagPromptBuilder(_fileReader, _randomSource);
        _uiDialog = uiDialog ?? new UiDialogService();
        _clipboard = clipboard ?? new ClipboardService();
        _process = process ?? new ProcessService();
        _dispatcher = dispatcher ?? new DispatcherService();

        _appDataStore.EnsureInitialized(AppContext.BaseDirectory, _errors);
        var root = _appDataStore.RootDir;
        _errors.Info("Using AppData RootDir: " + root);

        SearchViewModel = searchViewModel ?? BuildSearchViewModel();
        SearchViewModel.SelectedFiles.CollectionChanged += OnSelectedFilesChanged;

        CopyCommand = new RelayCommand("Copy", _ => Copy(), errorReporter: _errors);
        SendToSwarmUiCommand = new RelayCommand("SendToSwarmUi", _ => SendToSwarmUi(), errorReporter: _errors);
        SendBatchToSwarmUiCommand = new RelayCommand("SendBatchToSwarmUi", _ => SendBatchToSwarmUi(), errorReporter: _errors);
        RandomizeCommand = new RelayCommand("Randomize", _ => Randomize(), errorReporter: _errors);
        TestSwarmConnectionCommand = new AsyncRelayCommand("TestSwarmConnection", _ => TestSwarmConnection(), errorReporter: _errors);
        RefreshSwarmModelsCommand = new AsyncRelayCommand("RefreshSwarmModels", _ => RefreshSwarmModels(), errorReporter: _errors);
        RefreshSwarmLorasCommand = new RelayCommand("RefreshSwarmLoras", _ => RefreshSwarmLoras(), errorReporter: _errors);
        ShowImageOverlayCommand = new RelayCommand("ShowImageOverlay", p => ShowImageOverlay(p), errorReporter: _errors);
        HideImageOverlayCommand = new RelayCommand("HideImageOverlay", _ => HideImageOverlay(), errorReporter: _errors);
        ToggleOverlayDetailsCommand = new RelayCommand("ToggleOverlayDetails", _ => OverlayDetailsExpanded = !OverlayDetailsExpanded, errorReporter: _errors);
        OverlayPrevCommand = new RelayCommand("OverlayPrev", _ => NavigateOverlay(-1), errorReporter: _errors);
        OverlayNextCommand = new RelayCommand("OverlayNext", _ => NavigateOverlay(+1), errorReporter: _errors);
        OpenSwarmUiCommand = new RelayCommand("OpenSwarmUi", _ => OpenSwarmUi(), errorReporter: _errors);
        SaveOutputCommand = new RelayCommand("SaveOutput", _ => SaveOutput(), errorReporter: _errors);
        OpenFolderCommand = new RelayCommand("OpenFolder", _ => OpenFolder(), errorReporter: _errors);

        _errors.Info("MainViewModel ctor end");

        LoadUserSettings();
    }

    private SearchViewModel BuildSearchViewModel()
    {
        var tagIndexStore = new TagIndexStore(_fileSystem, _appDataStore);
        var stopWordsStore = new StopWordsStore(_fileSystem, _appDataStore);
        var lemmatizer = new Lemmatizer();
        var tokenizer = new TagTokenizer(lemmatizer);
        var tagIndexer = new TagIndexer(tagIndexStore, stopWordsStore, tokenizer, _appDataStore, _fileSystem, _clock);
        var tagSearchService = new TagSearchService(tagIndexStore, stopWordsStore, tokenizer);

        return new SearchViewModel(tagIndexer, tagSearchService, _errors);
    }





    private void EnablePromptGeneration()
    {
        if (_promptEnabled)
            return;

        _promptEnabled = true;
    }

    private void OnSelectedFilesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_promptEnabled)
        {
            if (SearchViewModel.SelectedFiles.Count == 0 && SearchViewModel.SelectedTags.Count == 0)
                return;

            EnablePromptGeneration();
        }

        QueueRecomputePrompt(immediate: true);
    }




    internal void QueueRecomputePrompt(bool immediate = false)
    {
        if (_suppressPromptRecompute || !_promptEnabled)
            return;

        QueueAutoSave();

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

    private void QueueAutoSave()
    {
        _autoSaveCts?.Cancel();
        _autoSaveCts?.Dispose();
        _autoSaveCts = new CancellationTokenSource();
        var token = _autoSaveCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(_autoSaveDebounce, token);
                if (token.IsCancellationRequested) return;

                SaveUserSettings();
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

    private void LoadUserSettings()
    {
        try
        {
            var s = _settingsStore.Load();
            if (s is null)
                return;

            SwarmUrl = s.SwarmUrl;
            SwarmToken = s.SwarmToken;

            SendSwarmSteps = s.SendSwarmSteps;
            SwarmSteps = s.SwarmSteps;

            SendSwarmCfgScale = s.SendSwarmCfgScale;
            SwarmCfgScale = s.SwarmCfgScale;

            SendSwarmSeed = s.SendSwarmSeed;

            SendSwarmModelOverride = s.SendSwarmModelOverride;
            SwarmSelectedModel = s.SwarmSelectedModel ?? "";

            SendSwarmLoras = s.SendSwarmLoras;
            SwarmSelectedLora1 = s.SwarmSelectedLora1 ?? "";
            SwarmLora1Weight = s.SwarmLora1Weight;
            SwarmSelectedLora2 = s.SwarmSelectedLora2 ?? "";
            SwarmLora2Weight = s.SwarmLora2Weight;

            BatchQty = Math.Clamp(s.BatchQty, 2, 50);
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
            var s = new UserSettings
            {
                SwarmUrl = SwarmUrl,
                SwarmToken = SwarmToken,

                SendSwarmSteps = SendSwarmSteps,
                SwarmSteps = SwarmSteps,

                SendSwarmCfgScale = SendSwarmCfgScale,
                SwarmCfgScale = SwarmCfgScale,

                SendSwarmSeed = SendSwarmSeed,

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

            _settingsStore.Save(s);
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
        if (_suppressPromptRecompute || !_promptEnabled)
            return;

        try
        {
            // NOTE (2025-12-22): SwarmUI accepts seed values of -1 (random) or any non-negative number.
            // Other negative numbers cause SwarmUI to error, so we clamp them to -1.
            var seedLong = seedOverride.HasValue ? seedOverride.Value : NormalizeSeedForSwarmUi(PromptSeedText);
            int? seed = seedLong is null
                ? null
                : (seedLong.Value <= int.MaxValue ? (int)seedLong.Value : int.MaxValue);

            var files = SearchViewModel.SelectedFiles.Select(file => file.Path).ToList();
            var result = _promptBuilder.Generate(files, seed);
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
        var seed = SendSwarmSeed ? NormalizeSeedForSwarmUi(ImageSeedText) : null;
        var batch = CreateBatchForRun(title: "Single", promptSnapshot: PromptText, seed: seed);
        _dispatcher.Invoke(() => AddRecentBatch(batch));
        _ = StartSwarmGenerationAsync(batch, PromptText, seed);
    }

    private void SendBatchToSwarmUi()
    {
        // Fire off multiple generations grouped into a single batch.
        // "Randomized prompts" here means: regenerate the PromptLoom prompt using a fresh seed each time.
        var imageSeed = SendSwarmSeed ? NormalizeSeedForSwarmUi(ImageSeedText) : null;
        var batch = CreateBatchForRun(title: $"Batch ×{BatchQty}", promptSnapshot: PromptText, seed: imageSeed);
        _dispatcher.Invoke(() => AddRecentBatch(batch));

        for (var i = 0; i < BatchQty; i++)
        {
            string prompt;
            long? seed = imageSeed;

            if (BatchRandomizePrompts)
            {
                var promptSeed = _random.Next(0, int.MaxValue);
                try
                {
                    var files = SearchViewModel.SelectedFiles.Select(file => file.Path).ToList();
                    var res = _promptBuilder.Generate(files, promptSeed);
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
            var baseUri = _swarmService.GetBaseUri(SwarmUrl);

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
                        await _swarmService.InterruptAllAsync(SwarmUrl, SwarmToken, otherSessions: false, ct: CancellationToken.None).ConfigureAwait(false);
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

            var (suggestedModel, w, h) = await _swarmService.GetSuggestedModelAndResolutionAsync(SwarmUrl, SwarmToken, cts.Token).ConfigureAwait(false);

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

            await foreach (var frame in _swarmService.GenerateText2ImageStreamAsync(SwarmUrl, SwarmToken, req, cts.Token).ConfigureAwait(false))
            {
                if (cts.IsCancellationRequested) break;

                if (SwarmWsParser.TryParseSwarmWsFrame(frame, out var status, out var progress01, out var step, out var steps, out var previewDataUrl, out var finalImageRef, out var seedUsed))
                {
                    _dispatcher.Invoke(() =>
                    {
                        if (item is null) return;

                        if (!string.IsNullOrWhiteSpace(status))
                            item.Status = status;

                        if (step is not null || steps is not null)
                        {
                            item.CurrentStep = step;
                            item.TotalSteps = steps;
                        }

                        if (seedUsed is not null)
                        {
                            // Keep the Prompt tab's seed display in sync with the actual SwarmUI seed.
                            SetImageSeedTextFromSwarm(seedUsed.Value);
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
        // Generate a new random seed and trigger prompt recomputation. Setting the prompt seed
        // property will cause RecomputePrompt() via its setter.
        // NOTE (2025-12-22): SwarmUI rejects negative seeds other than -1.
        // Keep randomized seeds non-negative so they are always valid for both PromptLoom and SwarmUI.
        EnablePromptGeneration();
        PromptSeedText = _random.Next(0, int.MaxValue).ToString();
        // When prefixes/suffixes are changed after randomization, recompute prompt using new random seed.
        QueueRecomputePrompt(immediate: true);
    }

    

    internal async Task TestSwarmConnection()
    {
        try
        {
            var status = await _swarmService.GetStatusAsync(SwarmUrl, SwarmToken, CancellationToken.None).ConfigureAwait(false);

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

    private static SwarmAssetCardViewModel BuildSwarmCard(string name, string kindLabel)
        => new SwarmAssetCardViewModel(name, kindLabel);

    internal async Task RefreshSwarmModels()
    {
        try
        {
            SwarmUiStatusText = "Loading models...";

            var models = await _swarmService.ListModelsAsync(SwarmUrl, SwarmToken, depth: 8, ct: CancellationToken.None).ConfigureAwait(false);
            var cards = models.Select(m => BuildSwarmCard(m, "Model")).ToList();

            _dispatcher.Invoke(() =>
            {
                SwarmModels.Clear();
                foreach (var card in cards)
                    SwarmModels.Add(card);

                if (!string.IsNullOrWhiteSpace(SwarmSelectedModel) &&
                    SwarmModels.Any(m => string.Equals(m.Name, SwarmSelectedModel, StringComparison.OrdinalIgnoreCase)))
                {
                    // keep user's selection
                }
                else if (SwarmModels.Count > 0)
                {
                    SwarmSelectedModel = SwarmModels[0].Name;
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

    internal async void RefreshSwarmLoras()
    {
        try
        {
            SwarmUiStatusText = "Loading LoRAs...";

            var loras = await _swarmService.ListLorasAsync(SwarmUrl, SwarmToken, depth: 8, ct: CancellationToken.None).ConfigureAwait(false);
            var cards = loras.Select(l => BuildSwarmCard(l, "LoRA")).ToList();

            _dispatcher.Invoke(() =>
            {
                SwarmLoras.Clear();
                foreach (var card in cards)
                    SwarmLoras.Add(card);

                if (!string.IsNullOrWhiteSpace(SwarmSelectedLora1) &&
                    SwarmLoras.Any(l => string.Equals(l.Name, SwarmSelectedLora1, StringComparison.OrdinalIgnoreCase)))
                {
                    // keep selection
                }
                else if (SwarmLoras.Count > 0)
                {
                    SwarmSelectedLora1 = SwarmLoras[0].Name;
                }

                if (!string.IsNullOrWhiteSpace(SwarmSelectedLora2) &&
                    SwarmLoras.Any(l => string.Equals(l.Name, SwarmSelectedLora2, StringComparison.OrdinalIgnoreCase)))
                {
                    // keep selection
                }
                else
                {
                    SwarmSelectedLora2 = "";
                }

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
            _fileSystem.CreateDirectory(_appDataStore.OutputDir);
            var ts = DateTime.Now;
            var baseName = $"prompt_{ts:yyyyMMdd_HHmmss}";
            var path = Path.Combine(_appDataStore.OutputDir, baseName + ".txt");
            _fileSystem.WriteAllText(path, PromptText);
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
            _process.Start(new ProcessStartInfo
            {
                FileName = _appDataStore.LibraryDir,
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

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
