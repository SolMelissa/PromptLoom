// CHANGE LOG
// - 2026-01-02 | Fix: Tag count shadowing | Avoid duplicate local names in UpdateTagCountsAsync.
// - 2026-01-02 | Request: Tag count scope | Use global counts for suggestions before selection.
// - 2026-01-02 | Request: Related tag relevance | Add related tag model and relevance updates.
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
            var result = await _tagIndexer.SyncAsync();
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
        var tag = NormalizeTag(ResolveTagName(parameter) ?? SearchQuery);
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

        var results = await _tagSearchService.SearchFilesAsync(SelectedTags.Select(tag => tag.Name).ToList());
        ReplaceCollection(Results, results);
        _currentFilePaths = results.Select(result => result.Path).ToList();
        await UpdateTagCountsAsync(SelectedTags, useAllFilesWhenEmpty: false);
        await UpdateTagCountsAsync(SuggestedTags, useAllFilesWhenEmpty: false);
        await RefreshRelatedTagsAsync(results);
        State = ResolveStateAfterSearch(results.Count);
    }

    private SearchState ResolveStateAfterSearch(int resultCount)
    {
        if (_totalFiles == 0)
            return SearchState.EmptyIndex;

        if (SelectedTags.Count > 0 && resultCount == 0)
            return SearchState.NoResults;

        return SearchState.Ready;
    }

    private static string NormalizeTag(string tag)
        => tag.Trim().ToLowerInvariant();

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

        ReplaceCollection(RelatedTags, related.Select(item => new RelatedTagItem(item.Name, item.RelevancePercent)));
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

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private static string FormatIndexSummary(TagIndexSyncResult result)
        => $"Index ready: {result.TotalFiles} files, {result.TotalTags} tags (added {result.AddedFiles}, updated {result.UpdatedFiles}, removed {result.RemovedFiles}).";
}
