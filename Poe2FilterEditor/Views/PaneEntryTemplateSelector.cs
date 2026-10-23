using System.Windows;
using System.Windows.Controls;
using Poe2FilterEditor.ViewModels;

namespace Poe2FilterEditor.Views
{
    /// <summary>
    /// 左栏列表：分组标题与规则使用不同模板。
    /// </summary>
    public sealed class PaneEntryTemplateSelector : DataTemplateSelector
    {
        public DataTemplate? SectionTemplate { get; set; }

        public DataTemplate? RuleTemplate { get; set; }

        public override DataTemplate? SelectTemplate(object? item, DependencyObject container)
        {
            return item is SectionEntry ? SectionTemplate : RuleTemplate;
        }
    }
}
