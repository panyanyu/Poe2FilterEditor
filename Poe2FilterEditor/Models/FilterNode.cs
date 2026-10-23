using System.Collections.Generic;
using System.Linq;

namespace Poe2FilterEditor.Models
{
    /// <summary>
    /// 过滤器文档中的一个有序节点：注释块 / 空行 / 规则 / 无法识别的原始行。
    /// 保留节点顺序即可实现「打开 → 保存」零丢失往返。
    /// </summary>
    public abstract class FilterNode
    {
        /// <summary>原始文件中的起始行号（从 1 开始）。</summary>
        public int LineNumber { get; set; }
    }

    /// <summary>连续的注释行。</summary>
    public sealed class CommentNode : FilterNode
    {
        public List<string> Lines { get; } = new();

        /// <summary>去掉分隔符后的正文，用于列表展示分组标题。</summary>
        public string Text
        {
            get
            {
                var content = Lines
                    .Select(l => l.TrimStart('#').Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l) && !l.All(ch => ch == '=' || ch == '-' || ch == ' '))
                    .ToList();
                return string.Join(" ", content);
            }
        }

        public bool HasContent => !string.IsNullOrWhiteSpace(Text);

        /// <summary>纯分隔线注释块（例如 # ============）。</summary>
        public bool IsSeparator => Lines.Count > 0 && Lines.All(l =>
        {
            var t = l.TrimStart('#').Trim();
            return t.Length == 0 || t.All(ch => ch == '=' || ch == '-' || ch == '*');
        });
    }

    /// <summary>连续空行。</summary>
    public sealed class BlankNode : FilterNode
    {
        public int Count { get; set; } = 1;
    }

    /// <summary>无法归类的原始行，写回时原样输出。</summary>
    public sealed class RawNode : FilterNode
    {
        public RawNode(string text) => Text = text;

        public string Text { get; set; }
    }
}
