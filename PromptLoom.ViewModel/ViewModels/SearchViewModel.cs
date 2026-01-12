// CHANGE LOG
// - 2026-03-09 | Request: Clear selected files | Add explicit command to clear selected file list.
// - 2026-03-09 | Request: Selected files persist | Keep selected files when tags change or searches refresh.
// - 2026-03-09 | Request: Indexing progress | Surface per-file indexing progress during background sync.
// - 2026-03-09 | Request: Background indexing | Run tag index sync on a background worker to avoid UI stalls.
// - 2026-03-06 | Request: TF-IDF relevance | Compute relevance percentages from TF-IDF scores.
// - 2026-03-06 | Fix: Tag sorting | Preserve tag items while reordering by count and relevance.
// - 2026-03-02 | Request: File relevance | Compute per-file relevance percentages for pills.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
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

    public TagDisplayItem(string name, int count = 0)
    {
        Name = name;
        _count = count;
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

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Related tag display item with relevance percentage.
/// </summary>
public sealed class RelatedTagItem
{
    public RelatedTagItem(string name, int relevancePercent)
    {
        Name = name;
        RelevancePercent = relevancePercent;
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
    private CancellationTokenSource? _suggestionCts;

    private string _searchQuery = string.Empty;
    private SearchState _state = SearchState.Ready;
    private string? _errorMessage;
    private string? _indexStatusMessage;
    private int _totalFiles = -1;
    private IReadOnlyList<string> _currentFilePaths = Array.Empty<string>();

    /// <summary>
    /// Creates a new search view model.
    /// </summary>
    public SearchViewModel(
        ITagIndexer tagIndexer,
        ITagSearchService tagSearchService,
        IErrorReporter? errors = null)
    {
        _tagIndexer = tagIndexer;
        _tagSearchService = tagSearchService;
        _errors = errors ?? new ErrorReporterAdapter();

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
        IndexStatusMessage = "Indexing tags...";
        State = SearchState.Indexing;

        try
        {
            var progress = new Progress<TagIndexProgress>(info =>
            {
                if (info.Total <= 0)
                    IndexStatusMessage = $"{info.Stage}...";
                else
                    IndexStatusMessage = $"{info.Stage} ({info.Processed}/{info.Total}, {info.Percent}%)";
            });

            var result = await Task.Run(async () => await _tagIndexer.SyncAsync(progress: progress));
            _totalFiles = result.TotalFiles;
            IndexStatusMessage = FormatIndexSummary(result);
            await RefreshResultsAsync();
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "SearchViewModel.RefreshIndex");
            ErrorMessage = ex.Message;
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
            SuggestedTags.Clear();
            return;
        }

        var cts = new CancellationTokenSource();
        _suggestionCts = cts;

        try
        {
            var suggestions = await _tagSearchService.SuggestTagsAsync(SearchQuery, SuggestionLimit, cts.Token);
            if (cts.IsCancellationRequested)
                return;

            var filtered = suggestions.Where(suggestion =>
                !SelectedTags.Any(selected => string.Equals(selected.Name, suggestion, StringComparison.OrdinalIgnoreCase)));

            ReplaceCollection(SuggestedTags, filtered.Select(tag => new TagDisplayItem(tag)));
            await UpdateTagCountsAsync(SuggestedTags, useAllFilesWhenEmpty: SelectedTags.Count == 0);
            SortTagCollection(SuggestedTags);
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _errors.Report(ex, "SearchViewModel.RefreshSuggestions");
            ErrorMessage = ex.Message;
            State = SearchState.Error;
        }
    }

    private async Task RefreshResultsAsync()
    {
        if (_totalFiles == 0)
        {
            Results.Clear();
            State = SearchState.EmptyIndex;
            return;
        }

        if (SelectedTags.Count == 0)
        {
            Results.Clear();
            RelatedTags.Clear();
            _currentFilePaths = Array.Empty<string>();
            State = ResolveStateAfterSearch(0);
            return;
        }

        var rawResults = await _tagSearchService.SearchFilesAsync(SelectedTags.Select(tag => tag.Name).ToList());
        var scoredResults = ApplyRelevanceScores(rawResults);
        var orderedResults = OrderResults(scoredResults);
        ReplaceCollection(Results, orderedResults);
        SyncSelectedFiles(orderedResults);
        _currentFilePaths = orderedResults.Select(result => result.Path).ToList();
        await UpdateTagCountsAsync(SelectedTags, useAllFilesWhenEmpty: false);
        await UpdateTagCountsAsync(SuggestedTags, useAllFilesWhenEmpty: false);
        SortTagCollection(SelectedTags);
        SortTagCollection(SuggestedTags);
        await RefreshRelatedTagsAsync(orderedResults);
        State = ResolveStateAfterSearch(orderedResults.Count);
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

    private async Task RefreshRelatedTagsAsync(IReadOnlyList<TagFileInfo> results)
    {
        if (SelectedTags.Count == 0 || results.Count == 0)
        {
            RelatedTags.Clear();
            return;
        }

        var related = await _tagSearchService.GetRelatedTagsAsync(
            SelectedTags.Select(tag => tag.Name).ToList(),
            results.Select(result => result.Path).ToList(),
            RelatedLimit);

        ReplaceCollection(
            RelatedTags,
            related
                .OrderByDescending(item => item.RelevancePercent)
                .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(item => new RelatedTagItem(item.Name, item.RelevancePercent)));
    }

    private async Task UpdateTagCountsAsync(IReadOnlyCollection<TagDisplayItem> tags, bool useAllFilesWhenEmpty)
    {
        if (tags.Count == 0)
            return;

        if (_currentFilePaths.Count == 0)
        {
            if (useAllFilesWhenEmpty)
            {
                var allTagNames = tags.Select(tag => tag.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                var allCounts = await _tagSearchService.CountTagReferencesAllFilesAsync(allTagNames);
                foreach (var tag in tags)
                    tag.Count = allCounts.TryGetValue(tag.Name, out var count) ? count : 0;
                return;
            }
            else
            {
                foreach (var tag in tags)
                    tag.Count = 0;
                return;
            }
        }

        var scopedTagNames = tags.Select(tag => tag.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var scopedCounts = await _tagSearchService.CountTagReferencesAsync(scopedTagNames, _currentFilePaths);
        foreach (var tag in tags)
            tag.Count = scopedCounts.TryGetValue(tag.Name, out var count) ? count : 0;
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

    private static string FormatIndexSummary(TagIndexSyncResult result)
        => $"Index ready: {result.TotalFiles} files, {result.TotalTags} tags (added {result.AddedFiles}, updated {result.UpdatedFiles}, removed {result.RemovedFiles}).";
}
