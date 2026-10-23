using Poe2FilterEditor.Infrastructure;

namespace Poe2FilterEditor.ViewModels
{
    /// <summary>
    /// 左栏列表条目：区块标题或规则。
    /// </summary>
    public abstract class PaneEntry : ObservableObject
    {
        public abstract bool IsSelectableRule { get; }
    }

    /// <summary>
    /// 注释块形成的分组标题。
    /// </summary>
    public sealed class SectionEntry : PaneEntry
    {
        public SectionEntry(string title, int lineNumber)
        {
            Title = title;
            LineNumber = lineNumber;
        }

        public string Title { get; }

        public int LineNumber { get; }

        public override bool IsSelectableRule => false;
    }
}
