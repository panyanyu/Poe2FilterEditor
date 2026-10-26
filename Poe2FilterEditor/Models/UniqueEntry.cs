using System;

namespace Poe2FilterEditor.Models
{
    /// <summary>
    /// 一件传奇物品：过滤器无法直接按传奇名匹配，只能用「基底 BaseType + Rarity Unique」。
    /// 因此这里同时保存传奇名与它所属的基底。
    /// </summary>
    public sealed class UniqueEntry
    {
        public UniqueEntry(string nameEn, string nameCn, string baseEn, string baseCn, string classEn = "")
        {
            NameEn = nameEn;
            NameCn = string.IsNullOrWhiteSpace(nameCn) ? nameEn : nameCn;
            BaseEn = baseEn;
            BaseCn = string.IsNullOrWhiteSpace(baseCn) ? baseEn : baseCn;
            ClassEn = classEn;
        }

        public string NameEn { get; }

        public string NameCn { get; }

        public string BaseEn { get; }

        public string BaseCn { get; }

        public string ClassEn { get; }

        public string Display => string.Equals(NameCn, NameEn, StringComparison.Ordinal)
            ? NameCn
            : $"{NameCn}  （{NameEn}）";

        public string BaseDisplay => string.Equals(BaseCn, BaseEn, StringComparison.Ordinal)
            ? BaseCn
            : $"{BaseCn}（{BaseEn}）";

        public string SubLine => $"基底：{BaseDisplay}";

        public string ToolTipText =>
            $"{NameCn}（{NameEn}）\n基底：{BaseCn}（{BaseEn}）\n\n过滤器按基底 + 传奇稀有度匹配，同基底的其它传奇也会一起命中。";

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return NameCn.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || NameEn.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || BaseCn.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || BaseEn.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString() => Display;
    }
}
