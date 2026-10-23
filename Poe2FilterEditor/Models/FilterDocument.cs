using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Poe2FilterEditor.Models
{
    /// <summary>
    /// 一个过滤器文件的内存表示：有序节点 + 文件元信息。
    /// </summary>
    public sealed class FilterDocument
    {
        public List<FilterNode> Nodes { get; } = new();

        public string? FilePath { get; set; }

        public bool HasBom { get; set; }

        /// <summary>原始换行符，保存时沿用。</summary>
        public string NewLine { get; set; } = "\r\n";

        public bool EndsWithNewLine { get; set; } = true;

        public bool IsDirty { get; set; }

        public Encoding Encoding { get; set; } = new UTF8Encoding(false);

        public IEnumerable<FilterRule> Rules => Nodes.OfType<FilterRule>();

        public int RuleCount => Nodes.Count(n => n is FilterRule);

        public string FileName => string.IsNullOrEmpty(FilePath) ? "未命名.filter" : Path.GetFileName(FilePath);

        public string DisplayPath => string.IsNullOrEmpty(FilePath) ? "尚未保存到文件" : FilePath!;

        /// <summary>紧邻该规则之前、可用于命名的注释节点。</summary>
        public string? TitleOf(FilterRule rule)
        {
            var index = Nodes.IndexOf(rule);
            for (var i = index - 1; i >= 0; i--)
            {
                switch (Nodes[i])
                {
                    case BlankNode:
                        continue;
                    case RawNode:
                        return null;
                    case CommentNode comment:
                        return comment.HasContent ? comment.Text : null;
                    default:
                        return null;
                }
            }

            return null;
        }
    }
}
