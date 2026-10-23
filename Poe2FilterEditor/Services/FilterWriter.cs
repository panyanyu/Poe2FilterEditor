using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Poe2FilterEditor.Models;

namespace Poe2FilterEditor.Services
{
    /// <summary>
    /// FilterDocument → 文本。未修改的规则原样输出，已修改的规则按统一格式输出。
    /// </summary>
    public static class FilterWriter
    {
        private const string Indent = "    ";

        public static string Write(FilterDocument document, bool normalizeOperators = false)
        {
            var lines = new List<string>();

            foreach (var node in document.Nodes)
            {
                switch (node)
                {
                    case CommentNode comment:
                        lines.AddRange(comment.Lines);
                        break;
                    case BlankNode blank:
                        for (var i = 0; i < Math.Max(blank.Count, 1); i++)
                        {
                            lines.Add(string.Empty);
                        }

                        break;
                    case RawNode raw:
                        lines.Add(raw.Text);
                        break;
                    case FilterRule rule:
                        lines.AddRange(WriteRule(rule, normalizeOperators));
                        break;

                }
            }

            var sb = new StringBuilder();
            for (var i = 0; i < lines.Count; i++)
            {
                sb.Append(lines[i]);
                if (i < lines.Count - 1 || document.EndsWithNewLine)
                {
                    sb.Append(document.NewLine);
                }
            }

            return sb.ToString();
        }

        /// <summary>生成单条规则的文本行（不含启用/停用前缀）。</summary>
        public static List<string> BuildRuleLines(FilterRule rule, bool normalizeOperators)
        {
            var lines = new List<string> { rule.IsShow ? "Show" : "Hide" };

            foreach (var condition in rule.Conditions)
            {
                lines.Add(Indent + FormatCondition(condition, normalizeOperators));
            }

            foreach (var action in rule.Actions)
            {
                lines.Add(Indent + FormatAction(action));
            }

            foreach (var extra in rule.ExtraLines)
            {
                lines.Add(Indent + extra);
            }

            return lines;
        }

        public static string FormatCondition(FilterCondition condition, bool normalizeOperators)
        {
            var definition = condition.Definition;
            var forceQuote = definition != null && definition.Kind == ConditionValueKind.Text;

            var parts = new List<string> { condition.Keyword };
            if (!normalizeOperators && condition.Op != CompareOp.Implicit)
            {
                parts.Add(condition.OpSymbol);
            }

            foreach (var value in condition.Values)
            {
                var quote = forceQuote || (definition == null && FilterLexer.NeedsQuotes(value));
                parts.Add(FilterLexer.FormatValue(value, quote));
            }

            var text = string.Join(" ", parts);
            return AppendComment(text, condition.InlineComment);
        }

        public static string FormatAction(FilterAction action)
        {
            var parts = new List<string> { action.Keyword };

            for (var i = 0; i < action.Args.Count; i++)
            {
                var arg = action.Args[i];
                var quote = action.IsSoundAction && i == 0;
                parts.Add(FilterLexer.FormatValue(arg, quote));
            }

            var text = string.Join(" ", parts);
            return AppendComment(text, action.InlineComment);
        }

        private static string AppendComment(string text, string? comment)
        {
            if (string.IsNullOrWhiteSpace(comment))
            {
                return text;
            }

            return $"{text}    # {comment}";
        }

        /// <summary>该规则最终写出的行（未修改时返回原始文本）。</summary>
        public static List<string> WriteRule(FilterRule rule, bool normalizeOperators)
        {
            if (!NeedsRegenerate(rule, normalizeOperators) && rule.OriginalLines.Count > 0)
            {
                return rule.OriginalLines;
            }

            var generated = BuildRuleLines(rule, normalizeOperators);
            if (rule.IsDisabled)
            {
                return generated.Select(line => line.Length == 0 ? "#" : "# " + line).ToList();
            }

            return generated;
        }

        public static bool NeedsRegenerate(FilterRule rule, bool normalizeOperators)
        {
            var needs = rule.IsModified || rule.IsDisabled;
            if (normalizeOperators)
            {
                needs |= rule.Conditions.Any(c => c.Op != CompareOp.Implicit);
            }

            return needs;
        }
    }
}
