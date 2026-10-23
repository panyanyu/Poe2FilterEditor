using System;
using System.Collections.Generic;
using System.Linq;

namespace Poe2FilterEditor.Models
{
    /// <summary>
    /// 一条 Show / Hide 规则。
    /// </summary>
    public sealed class FilterRule : FilterNode
    {
        public bool IsShow { get; set; } = true;

        /// <summary>整块被注释掉的规则（停用）。</summary>
        public bool IsDisabled { get; set; }

        public List<FilterCondition> Conditions { get; } = new();

        public List<FilterAction> Actions { get; } = new();

        /// <summary>解析器无法识别的行（不含缩进），写回时原样输出，保证不丢内容。</summary>
        public List<string> ExtraLines { get; } = new();

        /// <summary>原始文本行（含缩进），规则未被修改时原样写回。</summary>
        public List<string> OriginalLines { get; } = new();

        /// <summary>规则摘要（取自紧邻的注释行）。</summary>
        public string? Title { get; set; }

        public bool IsModified { get; set; }

        public void MarkModified() => IsModified = true;

        public FilterCondition? FindCondition(string keyword) =>
            Conditions.FirstOrDefault(c => c.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase));

        public IEnumerable<FilterCondition> FindConditions(string keyword) =>
            Conditions.Where(c => c.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase));

        public FilterCondition GetOrCreateCondition(string keyword)
        {
            var existing = FindCondition(keyword);
            if (existing != null)
            {
                return existing;
            }

            var created = new FilterCondition(keyword);
            Conditions.Add(created);
            MarkModified();
            return created;
        }

        public void RemoveCondition(string keyword)
        {
            var removed = Conditions.RemoveAll(c => c.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                MarkModified();
            }
        }

        /// <summary>设置单值条件；value 为 null/空则移除该条件。</summary>
        public void SetConditionValue(string keyword, string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                RemoveCondition(keyword);
                return;
            }

            var condition = GetOrCreateCondition(keyword);
            if (condition.Values.Count == 1 && condition.Values[0] == value)
            {
                return;
            }

            condition.Values.Clear();
            condition.Values.Add(value);
            MarkModified();
        }

        /// <summary>设置多值条件（Class / BaseType）。</summary>
        public void SetConditionValues(string keyword, IEnumerable<string> values)
        {
            var list = values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var condition = FindCondition(keyword);

            if (list.Count == 0)
            {
                if (condition != null)
                {
                    Conditions.Remove(condition);
                    MarkModified();
                }

                return;
            }

            condition = GetOrCreateCondition(keyword);
            if (condition.Values.SequenceEqual(list, StringComparer.OrdinalIgnoreCase))
            {
                return;
            }

            condition.Values.Clear();
            condition.Values.AddRange(list);
            MarkModified();
        }

        public FilterAction? GetAction(string keyword) =>
            Actions.FirstOrDefault(a => a.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase));

        public FilterAction GetOrCreateAction(string keyword)
        {
            var existing = GetAction(keyword);
            if (existing != null)
            {
                return existing;
            }

            var created = new FilterAction(keyword);
            Actions.Add(created);
            MarkModified();
            return created;
        }

        public void RemoveAction(string keyword)
        {
            var removed = Actions.RemoveAll(a => a.Keyword.Equals(keyword, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                MarkModified();
            }
        }

        public bool HasAction(string keyword) => GetAction(keyword) != null;

        /// <summary>
        /// 用于「规则重复」检测的签名。
        /// </summary>
        public string Signature()
        {
            var builder = new List<string> { IsShow ? "Show" : "Hide" };
            foreach (var condition in Conditions.OrderBy(c => c.Keyword, StringComparer.Ordinal))
            {
                builder.Add($"{condition.Keyword}{condition.OpSymbol}[{string.Join(",", condition.Values.OrderBy(v => v, StringComparer.OrdinalIgnoreCase))}]");
            }

            return string.Join("|", builder);
        }
    }
}
