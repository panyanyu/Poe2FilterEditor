using System.Collections.Generic;
using Poe2FilterEditor.Infrastructure;

namespace Poe2FilterEditor.ViewModels
{
    /// <summary>
    /// 预览面板中的一个文本块（注释块 / 空行 / 规则 / 原始行）。
    /// </summary>
    public sealed class PreviewBlockViewModel : ObservableObject
    {
        private bool _isSelected;

        public PreviewBlockViewModel(string text, RuleViewModel? rule, bool isComment)
        {
            Text = text;
            Rule = rule;
            IsComment = isComment;
        }

        public string Text { get; }

        public RuleViewModel? Rule { get; }

        public bool IsComment { get; }

        public bool IsRule => Rule != null;

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        public static PreviewBlockViewModel FromLines(IEnumerable<string> lines, RuleViewModel? rule, bool isComment)
        {
            return new PreviewBlockViewModel(string.Join("\n", lines), rule, isComment);
        }
    }
}
