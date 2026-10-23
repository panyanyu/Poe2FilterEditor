using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Poe2FilterEditor.Models;

namespace Poe2FilterEditor.Services
{
    /// <summary>
    /// 文本 → FilterDocument。解析过程绝不抛异常，无法识别的内容降级为 RawNode / ExtraLines 原样保留。
    /// </summary>
    public static class FilterParser
    {
        private static readonly Regex RuleHeader = new(@"^(Show|Hide)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex DisabledRuleHeader = new(@"^#+\s*(Show|Hide)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly string[] Operators = { "==", "!=", ">=", "<=", ">", "<" };

        public static FilterDocument Parse(string text, string? filePath = null)
        {
            var document = new FilterDocument
            {
                FilePath = filePath,
                NewLine = text.Contains("\r\n") ? "\r\n" : "\n"
            };

            var normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
            var raw = normalized.Split('\n').ToList();
            document.EndsWithNewLine = raw.Count > 0 && raw[^1].Length == 0;
            if (document.EndsWithNewLine)
            {
                raw.RemoveAt(raw.Count - 1);
            }

            var index = 0;
            while (index < raw.Count)
            {
                var line = raw[index];
                var trimmed = line.Trim();

                if (trimmed.Length == 0)
                {
                    var blankStart = index;
                    while (index < raw.Count && raw[index].Trim().Length == 0)
                    {
                        index++;
                    }

                    document.Nodes.Add(new BlankNode
                    {
                        LineNumber = blankStart + 1,
                        Count = index - blankStart
                    });
                    continue;
                }

                if (trimmed.StartsWith("#", StringComparison.Ordinal))
                {
                    index = ParseCommentBlock(raw, index, document);
                    continue;
                }

                var header = RuleHeader.Match(trimmed);
                if (header.Success)
                {
                    index = ParseRule(raw, index, header.Groups[1].Value, document);
                    continue;
                }

                document.Nodes.Add(new RawNode(line) { LineNumber = index + 1 });
                index++;
            }

            return document;
        }

        private static int ParseCommentBlock(List<string> raw, int start, FilterDocument document)
        {
            var end = start;
            while (end < raw.Count && raw[end].TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                end++;
            }

            var block = raw.GetRange(start, end - start);

            // 整块注释掉的规则（停用）
            if (block.Count > 0)
            {
                var firstUncommented = Uncomment(block[0]).Trim();
                if (DisabledRuleHeader.IsMatch(block[0]) || RuleHeader.IsMatch(firstUncommented))
                {
                    var header = RuleHeader.Match(firstUncommented);
                    if (header.Success)
                    {
                        var inner = block.Select(Uncomment).ToList();
                        var consumed = ParseRuleBody(inner, 0, header.Groups[1].Value, true, document, block);
                        return start + Math.Min(consumed, block.Count);
                    }
                }
            }

            var comment = new CommentNode { LineNumber = start + 1 };
            comment.Lines.AddRange(block);
            document.Nodes.Add(comment);
            return end;
        }

        private static int ParseRule(List<string> raw, int start, string showHide, FilterDocument document)
        {
            var body = new List<string>();
            var end = start + 1;
            while (end < raw.Count)
            {
                var line = raw[end];
                if (line.Trim().Length == 0)
                {
                    break;
                }

                if (line.TrimStart().StartsWith("#", StringComparison.Ordinal))
                {
                    break;
                }

                if (!char.IsWhiteSpace(line[0]))
                {
                    break;
                }

                body.Add(line);
                end++;
            }

            var rule = new FilterRule { LineNumber = start + 1, IsShow = !showHide.Equals("Hide", StringComparison.OrdinalIgnoreCase) };
            rule.OriginalLines.Add(raw[start]);
            rule.OriginalLines.AddRange(body);
            foreach (var line in body)
            {
                ParseBodyLine(rule, line.Trim());
            }

            document.Nodes.Add(rule);
            return end;
        }

        /// <summary>解析被整块注释的规则，返回消耗的行数。</summary>
        private static int ParseRuleBody(List<string> inner, int start, string showHide, bool disabled,
            FilterDocument document, List<string> originalCommentLines)
        {
            var rule = new FilterRule
            {
                LineNumber = start + 1,
                IsShow = !showHide.Equals("Hide", StringComparison.OrdinalIgnoreCase),
                IsDisabled = true
            };

            rule.OriginalLines.AddRange(originalCommentLines);

            var index = start + 1;
            while (index < inner.Count)
            {
                var line = inner[index];
                if (line.Trim().Length == 0 || !char.IsWhiteSpace(line[0]))
                {
                    break;
                }

                ParseBodyLine(rule, line.Trim());
                index++;
            }

            document.Nodes.Add(rule);
            return index;
        }

        private static string Uncomment(string line)
        {
            var trimmed = line.TrimStart();
            if (!trimmed.StartsWith("#", StringComparison.Ordinal))
            {
                return line;
            }

            trimmed = trimmed.Substring(1);
            if (trimmed.StartsWith(" ", StringComparison.Ordinal))
            {
                trimmed = trimmed.Substring(1);
            }

            return trimmed;
        }

        private static void ParseBodyLine(FilterRule rule, string content)
        {
            if (content.Length == 0)
            {
                return;
            }

            var (tokens, comment) = FilterLexer.Tokenize(content);
            if (tokens.Count == 0)
            {
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    rule.ExtraLines.Add("# " + comment);
                }

                return;
            }

            var keyword = tokens[0];
            var rest = tokens.Skip(1).ToList();
            var op = CompareOp.Implicit;

            if (rest.Count > 0 && Operators.Contains(rest[0]))
            {
                op = FilterCondition.ParseOp(rest[0]);
                rest = rest.Skip(1).ToList();
            }

            if (FilterCatalog.IsCondition(keyword))
            {
                var condition = new FilterCondition(keyword, op) { InlineComment = comment };
                condition.Values.AddRange(rest);
                rule.Conditions.Add(condition);
                return;
            }

            if (FilterCatalog.IsAction(keyword))
            {
                var action = new FilterAction(keyword) { InlineComment = comment };
                action.Args.AddRange(rest);
                rule.Actions.Add(action);
                return;
            }

            rule.ExtraLines.Add(string.IsNullOrWhiteSpace(comment) ? content : $"{content}    # {comment}");
        }
    }
}
