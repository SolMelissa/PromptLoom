/*
FIX: Small batches (under 4 images) should not consume card space.
CAUSE: All batches were rendered as large bordered cards, making single/duo/trio runs feel bulky.
CHANGE: Added a DataTemplateSelector that renders <4 as a labeled separator section, and 4+ as a batch card. 2025-12-24
*/

using System.Windows;
using System.Windows.Controls;
using PromptLoom.ViewModels;

namespace PromptLoom.Controls;

public sealed class RecentBatchTemplateSelector : DataTemplateSelector
{
    public DataTemplate? SmallBatchTemplate { get; set; }
    public DataTemplate? CardBatchTemplate { get; set; }

    public override DataTemplate? SelectTemplate(object item, DependencyObject container)
    {
        if (item is RecentSwarmBatchViewModel vm)
        {
            // "4 or more" => card. Otherwise => separator section.
            return (vm.Items?.Count ?? 0) >= 4 ? CardBatchTemplate : SmallBatchTemplate;
        }

        return base.SelectTemplate(item, container);
    }
}
