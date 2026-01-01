// CHANGE LOG
// - 2025-12-31 | Request: Model/LoRA cards | Provide card metadata for SwarmUI dropdowns.
using System;
using System.IO;

namespace PromptLoom.ViewModels;

/// <summary>
/// Presentation data for a SwarmUI model or LoRA selection card.
/// </summary>
public sealed class SwarmAssetCardViewModel
{
    /// <summary>
    /// Creates a new card for the given asset name and kind.
    /// </summary>
    public SwarmAssetCardViewModel(string name, string kindLabel)
    {
        Name = name ?? string.Empty;
        KindLabel = kindLabel ?? string.Empty;

        var normalized = Name.Replace('/', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);
        DisplayName = string.IsNullOrWhiteSpace(fileName)
            ? Name
            : Path.GetFileNameWithoutExtension(fileName);

        var dir = Path.GetDirectoryName(normalized) ?? string.Empty;
        PathHint = string.IsNullOrWhiteSpace(dir) ? string.Empty : dir.Replace(Path.DirectorySeparatorChar, '/');
    }

    public string Name { get; }
    public string DisplayName { get; }
    public string KindLabel { get; }
    public string PathHint { get; }
    public bool HasPathHint => !string.IsNullOrWhiteSpace(PathHint);
}
