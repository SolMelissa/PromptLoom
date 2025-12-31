/*
FIX: Move SwarmUI generation status/progress/cancel UI into each thumbnail item instead of global bar.
CAUSE: Global status couldn't represent concurrent/overlapping generations and forced HttpClient timeout hacks.
CHANGE: Added per-item viewmodel with CancelCommand and progress/status fields. 2025-12-22
*/

using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Media;

namespace PromptLoom.ViewModels;

public sealed class RecentSwarmImageViewModel : INotifyPropertyChanged
{
    private ImageSource? _thumbnail;
    public ImageSource? Thumbnail { get => _thumbnail; set { _thumbnail = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasThumbnail)); } }
    public bool HasThumbnail => Thumbnail is not null;

    public string Prompt { get; }
    private string? _model;
    public string? Model { get => _model; internal set { _model = value; OnPropertyChanged(); } }
    public long? Seed { get; internal set; }
    public int? Steps { get; internal set; }
    public double? CfgScale { get; internal set; }
    public string? LorasSummary { get; internal set; }
    public string PromptShort
    {
        get
        {
            var oneLine = (Prompt ?? string.Empty)
                .Replace("\r\n", " ")
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            return oneLine.Length <= 120 ? oneLine : oneLine[..120] + "…";
        }
    }

    private string _status = "Queued…";
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

    private double _progress;
    /// <summary>0..1</summary>
    public double Progress { get => _progress; set { _progress = value; OnPropertyChanged(); OnPropertyChanged(nameof(ProgressPercent)); } }
    public int ProgressPercent => (int)Math.Round(Math.Clamp(Progress, 0, 1) * 100);

    private bool _isIndeterminate = true;
    public bool IsIndeterminate { get => _isIndeterminate; set { _isIndeterminate = value; OnPropertyChanged(); } }

    private bool _isGenerating = true;
    public bool IsGenerating { get => _isGenerating; set { _isGenerating = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanCancel)); } }

    private bool _isComplete;
    public bool IsComplete { get => _isComplete; set { _isComplete = value; OnPropertyChanged(); } }

    private bool _isCancelled;
    public bool IsCancelled { get => _isCancelled; set { _isCancelled = value; OnPropertyChanged(); } }

    private bool _isFailed;
    public bool IsFailed { get => _isFailed; set { _isFailed = value; OnPropertyChanged(); } }

    public bool CanCancel => IsGenerating && !IsComplete && !IsCancelled;

    public ICommand CancelCommand { get; }

    public RecentSwarmImageViewModel(
        string prompt,
        Action onCancel,
        string? model = null,
        long? seed = null,
        int? steps = null,
        double? cfgScale = null,
        string? lorasSummary = null)
    {
        Prompt = prompt ?? "";
        Model = model;
        Seed = seed;
        Steps = steps;
        CfgScale = cfgScale;
        LorasSummary = lorasSummary;
        CancelCommand = new RelayCommand("CancelSwarmGen", _ => onCancel());
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
