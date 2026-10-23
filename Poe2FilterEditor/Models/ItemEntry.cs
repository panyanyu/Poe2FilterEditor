using System;

namespace Poe2FilterEditor.Models
{
    /// <summary>
    /// 一条物品/类别的中英映射。NameEn 为写入过滤器的内部名，NameCn 为国服展示名。
    /// </summary>
    public sealed class ItemEntry
    {
        public ItemEntry(string nameEn, string nameCn, string classEn, string classCn, bool isUnique = false, bool isUnknown = false)
        {
            NameEn = nameEn;
            NameCn = string.IsNullOrWhiteSpace(nameCn) ? nameEn : nameCn;
            ClassEn = classEn;
            ClassCn = string.IsNullOrWhiteSpace(classCn) ? classEn : classCn;
            IsUnique = isUnique;
            IsUnknown = isUnknown;
        }

        public string NameEn { get; }

        public string NameCn { get; }

        public string ClassEn { get; }

        public string ClassCn { get; }

        public bool IsUnique { get; }

        /// <summary>映射表中缺失、由解析器补出的占位条目。</summary>
        public bool IsUnknown { get; }

        /// <summary>列表展示：中文名（英文名）。</summary>
        public string Display => string.Equals(NameCn, NameEn, StringComparison.Ordinal)
            ? NameEn
            : $"{NameCn}  （{NameEn}）";

        public bool Matches(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return true;
            }

            return NameCn.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || NameEn.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || ClassCn.Contains(query, StringComparison.OrdinalIgnoreCase)
                   || ClassEn.Contains(query, StringComparison.OrdinalIgnoreCase);
        }

        public ItemEntry AsUnknown() => new(NameEn, NameEn, ClassEn, ClassCn, IsUnique, true);

        public override string ToString() => Display;
    }
}
