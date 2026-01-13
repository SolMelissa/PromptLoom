// CHANGE LOG
// - 2026-01-12 | Request: Indexing progress | Add elapsed-time heartbeat for long-running index stages.
// - 2026-01-12 | Request: Indexing responsiveness | Run tag queries on background threads and marshal UI updates.
// - 2026-01-12 | Request: Tag pills | Add color metadata to tags and file cards.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using PromptLoom.Services;

namespace PromptLoom.ViewModels;

/// <summary>
/// UI states for the tag search experience.
/// </summary>
public enum SearchState
{
    Indexing,
    Ready,
    EmptyIndex,
    NoResults,
    Error
}

/// <summary>
/// Tag display item with a reference count.
/// </summary>
public sealed class TagDisplayItem : INotifyPropertyChanged
{
    private int _count;
    private string _colorHex = string.Empty;

    public TagDisplayItem(string name, int count = 0, string colorHex = "")
    {
        Name = name;
        _count = count;
        _colorHex = colorHex ?? string.Empty;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Tag identifier.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Number of files in the current result list that reference this tag.
    /// </summary>
    public int Count
    {
        get => _count;
        set
        {
            if (_count == value)
                return;

            _count = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    /// <summary>
    /// Tag label with the count appended.
    /// </summary>
    public string DisplayText => $"{Name} ({Count})";

    /// <summary>
    /// Tag label for pill templates.
    /// </summary>
    public string Text => DisplayText;

    /// <summary>
    /// Stored color for the tag (hex).
    /// </summary>
    public string ColorHex
    {
        get => _colorHex;
        set
        {
            if (_colorHex == value)
                return;

            _colorHex = value ?? string.Empty;
            OnPropertyChanged();
        }
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Related tag display item with relevance percentage.
/// </summary>
public sealed class RelatedTagItem
{
    public RelatedTagItem(string name, int relevancePercent, string colorHex = "")
    {
        Name = name;
        RelevancePercent = relevancePercent;
        ColorHex = colorHex ?? string.Empty;
    }

    /// <summary>
    /// Tag identifier.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Relevance percentage for the current selection.
    /// </summary>
    public int RelevancePercent { get; }

    /// <summary>
    /// Tag label with the relevance percentage appended.
    /// </summary>
    public string DisplayText => $"{Name} ({RelevancePercent}%)";

    /// <summary>
    /// Tag label for pill templates.
    /// </summary>
    public string Text => DisplayText;

    /// <summary>
    /// Stored color for the tag (hex).
    /// </summary>
    public string ColorHex { get; }
}

/// <summary>
/// View model for tag-based search.
/// </summary>
public sealed class SearchViewModel : INotifyPropertyChanged
{
    private const int SuggestionLimit = 20;
    private const int RelatedLimit = 12;

    private readonly ITagIndexer _tagIndexer;
    private readonly ITagSearchService _tagSearchService;
    private readonly IErrorReporter _errors;
    private readonly IDispatcherService _dispatcher;
    private readonly string _libraryRoot;
    private CancellationTokenSource? _suggestionCts;
    private CancellationTokenSource? _resultsCts;
    private CancellationTokenSource? _indexTickerCts;
    private DateTime _indexStartUtc;
    private string _indexStatusBaseMessage = string.Empty;

    private string _searchQuery = string.Empty;
    private SearchState _state = SearchState.Ready;
    private string? _errorMessage;
    private string? _indexStatusMessage;
    private int _totalFiles = -1;
    private int _categoryColorCount = -1;
    private IReadOnlyList<string> _currentFilePaths = Array.Empty<string>();

    /// <summary>
    /// Creates a new search view model.
    /// </summary>
    public SearchViewModel(
        ITagIndexer tagIndexer,
        ITagSearchService tagSearchService,
        IErrorReporter? errors)
        : this(tagIndexer, tagSearchService, null, errors, null)
    {
    }

    /// <summary>
    /// Creates a new search view model.
    /// </summary>
    public SearchViewModel(
        ITagIndexer tagIndexer,
        ITagSearchService tagSearchService,
        string? libraryRoot = null,
        IErrorReporter? errors = null,
        IDispatcherService? dispatcher = null)
    {
        _tagIndexer = tagIndexer;
        _tagSearchService = tagSearchService;
        _errors = errors ?? new ErrorReporterAdapter();
        _libraryRoot = string.IsNullOrWhiteSpace(libraryRoot) ? string.Empty : libraryRoot;
        _dispatcher = dispatcher ?? new DispatcherService();

        RefreshCommand = new AsyncRelayCommand("Search.Refresh", _ => RefreshIndexAsync(), _ => State != SearchState.Indexing, _errors);
        AddTagCommand = new RelayCommand("Search.AddTag", AddTag, errorReporter: _errors);
        RemoveTagCommand = new RelayCommand("Search.RemoveTag", RemoveTag, errorReporter: _errors);
        ClearTagsCommand = new RelayCommand("Search.ClearTags", _ => ClearTags(), errorReporter: _errors);
        SelectFileCommand = new RelayCommand("Search.SelectFile", SelectFile, errorReporter: _errors);
        RemoveSelectedFileCommand = new RelayCommand("Search.RemoveSelectedFile", RemoveSelectedFile, errorReporter: _errors);
        MoveSelectedFileUpCommand = new RelayCommand("Search.MoveSelectedFileUp", MoveSelectedFileUp, errorReporter: _errors);
        MoveSelectedFileDownCommand = new RelayCommand("Search.MoveSelectedFileDown", MoveSelectedFileDown, errorReporter: _errors);
        ClearSelectedFilesCommand = new RelayCommand("Search.ClearSelectedFiles", _ => ClearSelectedFiles(), errorReporter: _errors);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Current search input text.
    /// </summary>
    public string SearchQuery
    {
        get => _searchQuery;
        set
        {
            if (_searchQuery == value)
                return;

            _searchQuery = value;
            OnPropertyChanged();
            _ = RefreshSuggestionsAsync();
        }
    }

    /// <summary>
    /// Current search state.
    /// </summary>
    public SearchState State
    {
        get => _state;
        private set
        {
            if (_state == value)
                return;

            _state = value;
            OnPropertyChanged();
            RefreshCommand.RaiseCanExecuteChanged();
        }
    }

    /// <summary>
    /// Last error message, if any.
    /// </summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value)
                return;

            _errorMessage = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Human-friendly status about the most recent indexing pass.
    /// </summary>
    public string? IndexStatusMessage
    {
        get => _indexStatusMessage;
        private set
        {
            if (_indexStatusMessage == value)
                return;

            _indexStatusMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasIndexStatusMessage));
        }
    }

    /// <summary>
    /// True when there is a status message to show.
    /// </summary>
    public bool HasIndexStatusMessage => !string.IsNullOrWhiteSpace(_indexStatusMessage);

    /// <summary>
    /// Selected tags for AND-based filtering.
    /// </summary>
    public ObservableCollection<TagDisplayItem> SelectedTags { get; } = new();

    /// <summary>
    /// Suggestions for the current query.
    /// </summary>
    public ObservableCollection<TagDisplayItem> SuggestedTags { get; } = new();

    /// <summary>
    /// Files matching the current selection.
    /// </summary>
    public ObservableCollection<TagFileInfo> Results { get; } = new();

    /// <summary>
    /// Related tags for the current selection.
    /// </summary>
    public ObservableCollection<RelatedTagItem> RelatedTags { get; } = new();

    /// <summary>
    /// Ordered list of selected files.
    /// </summary>
    public ObservableCollection<TagFileInfo> SelectedFiles { get; } = new();

    /// <summary>
    /// Command to refresh the tag index.
    /// </summary>
    public AsyncRelayCommand RefreshCommand { get; }

    /// <summary>
    /// Command to add a tag to the selection.
    /// </summary>
    public RelayCommand AddTagCommand { get; }

    /// <summary>
    /// Command to remove a tag from the selection.
    /// </summary>
    public RelayCommand RemoveTagCommand { get; }

    /// <summary>
    /// Command to clear all selected tags.
    /// </summary>
    public RelayCommand ClearTagsCommand { get; }

    /// <summary>
    /// Command to add a file to the selected list.
    /// </summary>
    public RelayCommand SelectFileCommand { get; }

    /// <summary>
    /// Command to remove a file from the selected list.
    /// </summary>
    public RelayCommand RemoveSelectedFileCommand { get; }

    /// <summary>
    /// Command to clear all selected files.
    /// </summary>
    public RelayCommand ClearSelectedFilesCommand { get; }

    /// <summary>
    /// Command to move a selected file up.
    /// </summary>
    public RelayCommand MoveSelectedFileUpCommand { get; }

    /// <summary>
    /// Command to move a selected file down.
    /// </summary>
    public RelayCommand MoveSelectedFileDownCommand { get; }

    /// <summary>
    /// Initializes the search state by running an index pass.
    /// </summary>
    public Task InitializeAsync() => RefreshIndexAsync();

    private async Task RefreshIndexAsync()
    {
        ErrorMessage = null;
        _indexStatusBaseMessage = "Indexing tags";
        IndexStatusMessage = $"{_indexStatusBaseMessage} (elapsed 00:00)";
        State = SearchState.Indexing;

        try
        {
            _indexStartUtc = DateTime.UtcNow;
            _indexTickerCts?.Cancel();
            var tickerCts = new CancellationTokenSource();
            _indexTickerCts = tickerCts;
            _ = Task.Run(async () =>
            {
                while (!tickerCts.Token.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(2), tickerCts.Token);
                    var elapsed = DateTime.UtcNow - _indexStartUtc;
                    await InvokeOnUiAsync(() =>
                    {
                        IndexStatusMessage = $"{_indexStatusBaseMessage} (elapsed {elapsed:mm\\:ss})";
                    });
                }
            }, tickerCts.Token);

            var progress = new Progress<TagIndexProgress>(info =>
            {
                _indexStatusBaseMessage = info.Total <= 0
                    ? info.Stage
                    : $"{info.Stage} ({info.Processed}/{info.Total}, {info.Percent}%)";
                if (info.Total <= 0)
                    IndexStatusMessage = $"{info.Stage}...";
                else
                    IndexStatusMessage = $"{info.Stage} ({info.Processed}/{info.Total}, {info.Percent}%)";
            });

            var result = await Task.Run(async () => await _tagIndexer.SyncAsync(progress: progress));
            _totalFiles = result.TotalFiles;
            _categoryColorCount = result.TotalCategoryColors;
            _indexTickerCts?.Cancel();
            IndexStatusMessage = FormatIndexSummary(result);
            await RefreshResultsAsync();
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "SearchViewModel.RefreshIndex");
            ErrorMessage = ex.Message;
            _indexTickerCts?.Cancel();
            IndexStatusMessage = "Indexing failed.";
            State = SearchState.Error;
        }
    }

    private void AddTag(object? parameter)
    {
        var tag = NormalizeUserInputTag(ResolveTagName(parameter) ?? SearchQuery);
        SearchQuery = string.Empty;
        if (tag.Length == 0)
            return;

        if (SelectedTags.Any(existing => string.Equals(existing.Name, tag, StringComparison.OrdinalIgnoreCase)))
            return;

        SelectedTags.Add(new TagDisplayItem(tag));
        _ = RefreshResultsAsync();
    }

    private void RemoveTag(object? parameter)
    {
        var tag = ResolveTagName(parameter);
        if (tag == null)
            return;

        var existing = SelectedTags.FirstOrDefault(value => string.Equals(value.Name, tag, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
            return;

        SelectedTags.Remove(existing);
        _ = RefreshResultsAsync();
    }

    private void ClearTags()
    {
        SelectedTags.Clear();
        RelatedTags.Clear();
        Results.Clear();
        _currentFilePaths = Array.Empty<string>();
        State = ResolveStateAfterSearch(0);
        _ = RefreshSuggestionsAsync();
    }

    private void ClearSelectedFiles()
    {
        SelectedFiles.Clear();
    }

    private async Task RefreshSuggestionsAsync()
    {
        _suggestionCts?.Cancel();

        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            await InvokeOnUiAsync(() => SuggestedTags.Clear());
            return;
        }

        var cts = new CancellationTokenSource();
        _suggestionCts = cts;

        try
        {
            var query = SearchQuery;
            var selectedNames = SelectedTags
                .Select(selected => selected.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var currentPaths = _currentFilePaths.ToList();
            var useAllFilesWhenEmpty = SelectedTags.Count == 0;

            var items = await Task.Run(async () =>
            {
                var suggestions = await _tagSearchService.SuggestTagsAsync(query, SuggestionLimit, cts.Token);
                cts.Token.ThrowIfCancellationRequested();

                var filtered = suggestions.Where(suggestion => !selectedNames.Contains(suggestion)).ToList();
                var colors = await LoadTagColorsAsync(filtered, cts.Token);
                var list = filtered.Select(tag =>
                    new TagDisplayItem(tag, colorHex: ResolveTagColor(colors, tag))).ToList();
                var counts = await LoadTagCountsAsync(filtered, currentPaths, useAllFilesWhenEmpty, cts.Token);
                ApplyTagCounts(counts, list);

                return list
                    .OrderByDescending(tag => tag.Count)
                    .ThenBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }, cts.Token);

            if (cts.IsCancellationRequested)
                return;

            await InvokeOnUiAsync(() => ReplaceCollection(SuggestedTags, items));
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "SearchViewModel.RefreshSuggestions");
            await InvokeOnUiAsync(() =>
            {
                ErrorMessage = ex.Message;
                State = SearchState.Error;
            });
        }
    }

    private async Task RefreshResultsAsync()
    {
        if (_totalFiles == 0)
        {
            await InvokeOnUiAsync(() =>
            {
                Results.Clear();
                State = SearchState.EmptyIndex;
            });
            return;
        }

        if (SelectedTags.Count == 0)
        {
            await InvokeOnUiAsync(() =>
            {
                Results.Clear();
                RelatedTags.Clear();
                _currentFilePaths = Array.Empty<string>();
                State = ResolveStateAfterSearch(0);
            });
            return;
        }

        _resultsCts?.Cancel();
        var cts = new CancellationTokenSource();
        _resultsCts = cts;

        var selectedNames = SelectedTags.Select(tag => tag.Name).ToList();
        var suggestedNames = SuggestedTags.Select(tag => tag.Name).ToList();

        try
        {
            var payload = await Task.Run(() =>
                BuildSearchPayloadAsync(selectedNames, suggestedNames, cts.Token), cts.Token);

            if (cts.IsCancellationRequested)
                return;

            await InvokeOnUiAsync(() =>
            {
                ReplaceCollection(Results, payload.Results);
                SyncSelectedFiles(payload.Results);
                _currentFilePaths = payload.Results.Select(result => result.Path).ToList();
                ApplyTagCounts(payload.SelectedTagCounts, SelectedTags);
                ApplyTagCounts(payload.SuggestedTagCounts, SuggestedTags);
                ApplyTagColors(payload.SelectedTagColors, SelectedTags);
                ApplyTagColors(payload.SuggestedTagColors, SuggestedTags);
                SortTagCollection(SelectedTags);
                SortTagCollection(SuggestedTags);
                ReplaceCollection(RelatedTags, payload.RelatedTags);
                State = ResolveStateAfterSearch(payload.Results.Count);
            });
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "SearchViewModel.RefreshResults");
            await InvokeOnUiAsync(() =>
            {
                ErrorMessage = ex.Message;
                State = SearchState.Error;
            });
        }
    }

    private SearchState ResolveStateAfterSearch(int resultCount)
    {
        if (_totalFiles == 0)
            return SearchState.EmptyIndex;

        if (SelectedTags.Count > 0 && resultCount == 0)
            return SearchState.NoResults;

        return SearchState.Ready;
    }

    private string NormalizeUserInputTag(string tag)
        => _tagSearchService.NormalizeTag(tag);

    private static string? ResolveTagName(object? parameter)
    {
        return parameter switch
        {
            TagDisplayItem item => item.Name,
            RelatedTagItem item => item.Name,
            string text => text,
            _ => null
        };
    }

    private void SelectFile(object? parameter)
    {
        var file = ResolveFile(parameter);
        if (file == null)
            return;

        if (SelectedFiles.Any(existing => string.Equals(existing.Path, file.Path, StringComparison.OrdinalIgnoreCase)))
            return;

        SelectedFiles.Add(file);
    }

    private void RemoveSelectedFile(object? parameter)
    {
        var file = ResolveFile(parameter);
        if (file == null)
            return;

        var existing = SelectedFiles.FirstOrDefault(item => string.Equals(item.Path, file.Path, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
            return;

        SelectedFiles.Remove(existing);
    }

    private void MoveSelectedFileUp(object? parameter)
        => MoveSelectedFile(parameter, -1);

    private void MoveSelectedFileDown(object? parameter)
        => MoveSelectedFile(parameter, 1);

    private void MoveSelectedFile(object? parameter, int offset)
    {
        var file = ResolveFile(parameter);
        if (file == null)
            return;

        var index = GetSelectedFileIndex(file);
        if (index < 0)
            return;

        var targetIndex = index + offset;
        if (targetIndex < 0 || targetIndex >= SelectedFiles.Count)
            return;

        SelectedFiles.Move(index, targetIndex);
    }

    private int GetSelectedFileIndex(TagFileInfo file)
    {
        for (var i = 0; i < SelectedFiles.Count; i++)
        {
            if (string.Equals(SelectedFiles[i].Path, file.Path, StringComparison.OrdinalIgnoreCase))
                return i;
        }

        return -1;
    }

    private static TagFileInfo? ResolveFile(object? parameter)
    {
        return parameter switch
        {
            TagFileInfo file => file,
            _ => null
        };
    }

    private static IReadOnlyList<TagFileInfo> ApplyRelevanceScores(IReadOnlyList<TagFileInfo> results)
    {
        if (results.Count == 0)
            return results;

        var maxScore = results.Max(result => result.RelevanceScore);
        if (maxScore <= 0)
            return results.Select(result => result with { RelevancePercent = 0 }).ToList();

        return results
            .Select(result => result with
            {
                RelevancePercent = (int)Math.Round(
                    result.RelevanceScore / maxScore * 100d,
                    MidpointRounding.AwayFromZero)
            })
            .ToList();
    }

    private void SyncSelectedFiles(IReadOnlyList<TagFileInfo> results)
    {
        if (SelectedFiles.Count == 0)
            return;

        var lookup = results.ToDictionary(result => result.Path, StringComparer.OrdinalIgnoreCase);
        for (var i = SelectedFiles.Count - 1; i >= 0; i--)
        {
            var selected = SelectedFiles[i];
            if (lookup.TryGetValue(selected.Path, out var updated) && !Equals(selected, updated))
                SelectedFiles[i] = updated;
        }
    }

    private async Task<SearchRefreshPayload> BuildSearchPayloadAsync(
        IReadOnlyList<string> selectedTags,
        IReadOnlyList<string> suggestedTags,
        CancellationToken ct)
    {
        var rawResults = await _tagSearchService.SearchFilesAsync(selectedTags, ct);
        var scoredResults = ApplyRelevanceScores(rawResults);
        var orderedResults = OrderResults(scoredResults);
        var enrichedResults = await EnrichResultsAsync(orderedResults, ct);

        var resultPaths = enrichedResults.Select(result => result.Path).ToList();
        var selectedTagCounts = await LoadTagCountsAsync(selectedTags, resultPaths, useAllFilesWhenEmpty: false, ct);
        var suggestedTagCounts = await LoadTagCountsAsync(suggestedTags, resultPaths, useAllFilesWhenEmpty: false, ct);
        var selectedTagColors = await LoadTagColorsAsync(selectedTags, ct);
        var suggestedTagColors = await LoadTagColorsAsync(suggestedTags, ct);
        var relatedTags = await BuildRelatedTagsAsync(selectedTags, orderedResults, ct);

        return new SearchRefreshPayload(
            enrichedResults,
            relatedTags,
            selectedTagCounts,
            suggestedTagCounts,
            selectedTagColors,
            suggestedTagColors);
    }

    private async Task<IReadOnlyList<RelatedTagItem>> BuildRelatedTagsAsync(
        IReadOnlyCollection<string> selectedTags,
        IReadOnlyList<TagFileInfo> results,
        CancellationToken ct)
    {
        if (selectedTags.Count == 0 || results.Count == 0)
            return Array.Empty<RelatedTagItem>();

        var related = await _tagSearchService.GetRelatedTagsAsync(
            selectedTags,
            results.Select(result => result.Path).ToList(),
            RelatedLimit,
            ct);

        var relatedList = related
            .OrderByDescending(item => item.RelevancePercent)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
        var colors = await LoadTagColorsAsync(relatedList.Select(item => item.Name).ToList(), ct);

        return relatedList
            .Select(item => new RelatedTagItem(item.Name, item.RelevancePercent, ResolveTagColor(colors, item.Name)))
            .ToList();
    }

    private async Task<IReadOnlyList<TagFileInfo>> EnrichResultsAsync(
        IReadOnlyList<TagFileInfo> results,
        CancellationToken ct)
    {
        if (results.Count == 0 || string.IsNullOrWhiteSpace(_libraryRoot))
            return results;

        var filePaths = results.Select(result => result.Path).ToList();
        var topTags = await _tagSearchService.GetTopTagsByContentAsync(filePaths, 3, ct);
        var allTopTags = topTags.Values.SelectMany(tags => tags).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var tagColors = await LoadTagColorsAsync(allTopTags, ct);

        var folderPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var relativeFolders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var result in results)
        {
            var relativePath = Path.GetRelativePath(_libraryRoot, result.Path);
            var folder = Path.GetDirectoryName(relativePath) ?? string.Empty;
            if (folder == ".")
                folder = string.Empty;

            relativeFolders[result.Path] = folder;
            AddFolderPaths(folder, folderPaths);
        }

        var colors = await _tagSearchService.GetCategoryColorsAsync(folderPaths, ct);

        return results.Select(result =>
        {
            relativeFolders.TryGetValue(result.Path, out var folder);
            var tags = topTags.TryGetValue(result.Path, out var list) ? list : Array.Empty<string>();
            var pathColors = BuildPathColors(folder ?? string.Empty, colors);
            var pills = tags.Select(tag => new TagPill(tag, ResolveTagColor(tagColors, tag))).ToList();
            return result with
            {
                RelativeFolderPath = folder ?? string.Empty,
                TopTags = tags,
                PathColors = pathColors,
                TagPills = pills
            };
        }).ToList();
    }

    private static void AddFolderPaths(string relativeFolder, ISet<string> target)
    {
        if (string.IsNullOrWhiteSpace(relativeFolder))
            return;

        var segments = relativeFolder.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        var current = string.Empty;
        foreach (var segment in segments)
        {
            current = string.IsNullOrEmpty(current) ? segment : Path.Combine(current, segment);
            target.Add(current);
        }
    }

    private static IReadOnlyList<string> BuildPathColors(
        string relativeFolder,
        IReadOnlyDictionary<string, string> colors)
    {
        if (string.IsNullOrWhiteSpace(relativeFolder))
            return Array.Empty<string>();

        var segments = relativeFolder.Split(
            new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
            StringSplitOptions.RemoveEmptyEntries);

        var list = new List<string>(segments.Length);
        var current = string.Empty;
        foreach (var segment in segments)
        {
            current = string.IsNullOrEmpty(current) ? segment : Path.Combine(current, segment);
            if (colors.TryGetValue(current, out var color))
                list.Add(color);
        }

        return list;
    }

    private async Task<IReadOnlyDictionary<string, int>> LoadTagCountsAsync(
        IReadOnlyCollection<string> tagNames,
        IReadOnlyList<string> filePaths,
        bool useAllFilesWhenEmpty,
        CancellationToken ct)
    {
        if (tagNames.Count == 0)
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var distinctNames = tagNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (filePaths.Count == 0)
        {
            if (!useAllFilesWhenEmpty)
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            return await _tagSearchService.CountTagReferencesAllFilesAsync(distinctNames, ct);
        }

        return await _tagSearchService.CountTagReferencesAsync(distinctNames, filePaths, ct);
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadTagColorsAsync(
        IReadOnlyCollection<string> tagNames,
        CancellationToken ct)
    {
        if (tagNames.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        var distinctNames = tagNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return await _tagSearchService.GetTagColorsAsync(distinctNames, ct);
    }

    private static void ApplyTagCounts(IReadOnlyDictionary<string, int> counts, IEnumerable<TagDisplayItem> tags)
    {
        foreach (var tag in tags)
            tag.Count = counts.TryGetValue(tag.Name, out var count) ? count : 0;
    }

    private static void ApplyTagColors(IReadOnlyDictionary<string, string> colors, IEnumerable<TagDisplayItem> tags)
    {
        foreach (var tag in tags)
            tag.ColorHex = ResolveTagColor(colors, tag.Name);
    }

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        foreach (var item in items)
            target.Add(item);
    }

    private static IReadOnlyList<TagFileInfo> OrderResults(IReadOnlyList<TagFileInfo> results)
    {
        if (results.Count <= 1)
            return results;

        return results
            .OrderByDescending(result => result.RelevancePercent)
            .ThenBy(result => result.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void SortTagCollection(ObservableCollection<TagDisplayItem> tags)
    {
        if (tags.Count <= 1)
            return;

        ReplaceCollection(
            tags,
            tags
                .OrderByDescending(tag => tag.Count)
                .ThenBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)
                .ToList());
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private Task InvokeOnUiAsync(Action action)
    {
        if (_dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        var tcs = new TaskCompletionSource<bool>();
        _dispatcher.BeginInvoke(() =>
        {
            try
            {
                action();
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        return tcs.Task;
    }

    private static string ResolveTagColor(IReadOnlyDictionary<string, string> colorMap, string tag)
        => colorMap.TryGetValue(tag, out var color) ? color : string.Empty;

    private sealed record SearchRefreshPayload(
        IReadOnlyList<TagFileInfo> Results,
        IReadOnlyList<RelatedTagItem> RelatedTags,
        IReadOnlyDictionary<string, int> SelectedTagCounts,
        IReadOnlyDictionary<string, int> SuggestedTagCounts,
        IReadOnlyDictionary<string, string> SelectedTagColors,
        IReadOnlyDictionary<string, string> SuggestedTagColors);

    private static string FormatIndexSummary(TagIndexSyncResult result)
    {
        var colorsPart = result.TotalCategoryColors >= 0
            ? $" Colors: {result.TotalCategoryColors}."
            : string.Empty;
        return $"Index ready: {result.TotalFiles} files, {result.TotalTags} tags (added {result.AddedFiles}, updated {result.UpdatedFiles}, removed {result.RemovedFiles}).{colorsPart}";
    }
}
