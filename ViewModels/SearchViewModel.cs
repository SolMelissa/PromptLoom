// CHANGE LOG
// - 2026-01-02 | Request: Tag indexing status | Add status message for indexing feedback.
// - 2026-01-02 | Fix: Clear search input on add | Reset SearchQuery after adding tags.
// - 2026-01-02 | Request: Tag search view model | Add MVVM state, suggestions, and selection logic.
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
/// View model for tag-based search.
/// </summary>
public sealed class SearchViewModel : INotifyPropertyChanged
{
    private const int SuggestionLimit = 20;

    private readonly ITagIndexer _tagIndexer;
    private readonly ITagSearchService _tagSearchService;
    private readonly IErrorReporter _errors;
    private CancellationTokenSource? _suggestionCts;

    private string _searchQuery = string.Empty;
    private SearchState _state = SearchState.Ready;
    private string? _errorMessage;
    private string? _indexStatusMessage;
    private int _totalFiles = -1;

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
    public ObservableCollection<string> SelectedTags { get; } = new();

    /// <summary>
    /// Suggestions for the current query.
    /// </summary>
    public ObservableCollection<string> SuggestedTags { get; } = new();

    /// <summary>
    /// Files matching the current selection.
    /// </summary>
    public ObservableCollection<TagFileInfo> Results { get; } = new();

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
        var tag = NormalizeTag(parameter as string ?? SearchQuery);
        SearchQuery = string.Empty;
        if (tag.Length == 0)
            return;

        if (SelectedTags.Any(existing => string.Equals(existing, tag, StringComparison.OrdinalIgnoreCase)))
            return;

        SelectedTags.Add(tag);
        _ = RefreshResultsAsync();
    }

    private void RemoveTag(object? parameter)
    {
        if (parameter is not string tag)
            return;

        var existing = SelectedTags.FirstOrDefault(value => string.Equals(value, tag, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
            return;

        SelectedTags.Remove(existing);
        _ = RefreshResultsAsync();
    }

    private void ClearTags()
    {
        SelectedTags.Clear();
        Results.Clear();
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
                !SelectedTags.Any(selected => string.Equals(selected, suggestion, StringComparison.OrdinalIgnoreCase)));

            ReplaceCollection(SuggestedTags, filtered);
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
            State = ResolveStateAfterSearch(0);
            return;
        }

        var results = await _tagSearchService.SearchFilesAsync(SelectedTags.ToList());
        ReplaceCollection(Results, results);
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
