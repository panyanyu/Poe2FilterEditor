using System;
using System.Collections.Generic;

namespace Poe2FilterEditor.Models
{
    public enum CompareOp
    {
        /// <summary>隐式匹配（标准写法）。</summary>
        Implicit,
        Equal,
        NotEqual,
        GreaterOrEqual,
        LessOrEqual,
        Greater,
        Less
    }

    /// <summary>
    /// 规则中的一条条件，例如 <c>BaseType "Divine Orb"</c> 或 <c>DropLevel >= 70</c>。
    /// Values 中保存的是英文内部名（写入文件用），界面翻译在 ViewModel 层完成。
    /// </summary>
    public sealed class FilterCondition
    {
        public FilterCondition(string keyword, CompareOp op = CompareOp.Implicit)
        {
            Keyword = keyword;
            Op = op;
        }

        public string Keyword { get; set; }

        public CompareOp Op { get; set; }

        public List<string> Values { get; set; } = new();

        public string? InlineComment { get; set; }

        public ConditionDefinition? Definition =>
            FilterCatalog.TryGetCondition(Keyword, out var def) ? def : null;

        public bool IsKnown => Definition != null;

        public string FirstValue => Values.Count > 0 ? Values[0] : string.Empty;

        public int? FirstValueAsInt => int.TryParse(FirstValue, out var value) ? value : null;

        public bool? FirstValueAsBool => bool.TryParse(FirstValue, out var value) ? value : null;

        public static readonly IReadOnlyDictionary<CompareOp, string> OpSymbols = new Dictionary<CompareOp, string>
        {
            [CompareOp.Implicit] = string.Empty,
            [CompareOp.Equal] = "==",
            [CompareOp.NotEqual] = "!=",
            [CompareOp.GreaterOrEqual] = ">=",
            [CompareOp.LessOrEqual] = "<=",
            [CompareOp.Greater] = ">",
            [CompareOp.Less] = "<"
        };

        public static CompareOp ParseOp(string token) => token switch
        {
            "==" => CompareOp.Equal,
            "!=" => CompareOp.NotEqual,
            ">=" => CompareOp.GreaterOrEqual,
            "<=" => CompareOp.LessOrEqual,
            ">" => CompareOp.Greater,
            "<" => CompareOp.Less,
            _ => CompareOp.Implicit
        };

        public string OpSymbol => OpSymbols[Op];

        public FilterCondition Clone() => new(Keyword, Op)
        {
            Values = new List<string>(Values),
            InlineComment = InlineComment
        };

        public override string ToString() => Values.Count == 0 ? Keyword : $"{Keyword} {string.Join(" ", Values)}";
    }
}
