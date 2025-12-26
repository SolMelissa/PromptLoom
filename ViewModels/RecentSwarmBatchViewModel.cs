/*
FIX: Recent image history needed to group related generations together (batch runs) instead of a single flat strip.
CAUSE: Flat list made it impossible to treat a batch as a cohesive unit and to present batch-scoped metadata cleanly.
CHANGE: Added RecentSwarmBatchViewModel to hold a batch and its generated image items. 2025-12-24
*/
// FIX: Make batch timestamps testable by accepting an IClock dependency.
// CAUSE: DateTime.Now in the property initializer made tests time-dependent.
// CHANGE: Use IClock with a default SystemClock. 2025-12-25

using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using PromptLoom.Services;

namespace PromptLoom.ViewModels;

public sealed class RecentSwarmBatchViewModel : INotifyPropertyChanged
{
    public Guid BatchId { get; } = Guid.NewGuid();
    public DateTime CreatedAt { get; }

    public string Title { get; }
    public string Prompt { get; }
    public string SettingsSummary { get; }

    private bool _isCard;
    /// <summary>True when this batch should render as a card (4+ images). Smaller batches render as a labeled separator.</summary>
    public bool IsCard { get => _isCard; private set { _isCard = value; OnPropertyChanged(); } }

    /// <summary>
    /// Short hint of the prompt used for the batch (for identifying cards quickly).
    /// UI requirement: this should always be a single line (no embedded line breaks).
    /// </summary>
    public string PromptSnippet
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Prompt))
                return string.Empty;

            // Normalize to one line for the Prompt tab UI.
            var oneLine = Prompt
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();

            return oneLine.Length <= 90 ? oneLine : oneLine[..90] + "…";
        }
    }

    public ObservableCollection<RecentSwarmImageViewModel> Items { get; } = new();

    /// <summary>
    /// Creates a new batch view model with an optional clock for testability.
    /// </summary>
    public RecentSwarmBatchViewModel(string title, string prompt, string settingsSummary, IClock? clock = null)
    {
        CreatedAt = (clock ?? new SystemClock()).Now;
        Title = string.IsNullOrWhiteSpace(title) ? "Batch" : title;
        Prompt = prompt ?? string.Empty;
        SettingsSummary = settingsSummary ?? string.Empty;

        Items.CollectionChanged += OnItemsCollectionChanged;
        RecomputeFlags();
    }

    private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        RecomputeFlags();
        OnPropertyChanged(nameof(PromptSnippet));
    }

    private void RecomputeFlags()
    {
        IsCard = Items.Count >= 4;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
