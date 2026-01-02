// CHANGE LOG
// - 2026-03-02 | Request: Deduplicate prompt panel | Add code-behind for shared prompt panel control.
using System.Windows.Controls;

namespace PromptLoom.Controls;

/// <summary>
/// Shared prompt panel UI for both wide and stacked layouts.
/// </summary>
public partial class PromptPanel : UserControl
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PromptPanel"/> class.
    /// </summary>
    public PromptPanel()
    {
        InitializeComponent();
    }
}
