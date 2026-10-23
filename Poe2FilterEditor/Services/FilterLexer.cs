using System.Collections.Generic;
using System.Text;

namespace Poe2FilterEditor.Services
{
    /// <summary>
    /// 引号感知的过滤器行分词器。支持 <c>"Two Hand Swords"</c> 这类带空格的值与行尾 <c>#</c> 注释。
    /// </summary>
    public static class FilterLexer
    {
        public static (List<string> Tokens, string? InlineComment) Tokenize(string line)
        {
            var tokens = new List<string>();
            string? comment = null;
            var builder = new StringBuilder();
            var inQuotes = false;
            var hasToken = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (inQuotes)
                {
                    if (ch == '\\' && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        builder.Append('"');
                        i++;
                        continue;
                    }

                    if (ch == '"')
                    {
                        inQuotes = false;
                        continue;
                    }

                    builder.Append(ch);
                    continue;
                }

                if (ch == '"')
                {
                    inQuotes = true;
                    hasToken = true;
                    continue;
                }

                if (ch == '#')
                {
                    comment = line.Substring(i + 1).Trim();
                    break;
                }

                if (char.IsWhiteSpace(ch))
                {
                    if (hasToken || builder.Length > 0)
                    {
                        tokens.Add(builder.ToString());
                        builder.Clear();
                        hasToken = false;
                    }

                    continue;
                }

                builder.Append(ch);
                hasToken = true;
            }

            if (builder.Length > 0 || hasToken)
            {
                tokens.Add(builder.ToString());
            }

            if (comment != null && comment.Length == 0)
            {
                comment = null;
            }

            return (tokens, comment);
        }

        /// <summary>需要加引号的值：包含空白或为空；含其它特殊符号时也加引号更安全。</summary>
        public static bool NeedsQuotes(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            foreach (var ch in value)
            {
                if (char.IsWhiteSpace(ch) || ch == '#')
                {
                    return true;
                }
            }

            return false;
        }

        public static string Quote(string value) => '"' + value.Replace("\"", "\\\"") + '"';

        public static string FormatValue(string value, bool forceQuote)
        {
            if (forceQuote || NeedsQuotes(value))
            {
                return Quote(value);
            }

            return value;
        }
    }
}
