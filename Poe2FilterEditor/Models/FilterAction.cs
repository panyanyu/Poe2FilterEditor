using System;
using System.Collections.Generic;

namespace Poe2FilterEditor.Models
{
    /// <summary>
    /// 规则中的一条动作，例如 <c>SetBorderColor 255 120 0 255</c> 或 <c>CustomAlertSound "sounds/a.mp3" 300</c>。
    /// </summary>
    public sealed class FilterAction
    {
        public FilterAction(string keyword)
        {
            Keyword = keyword;
        }

        public string Keyword { get; set; }

        public List<string> Args { get; set; } = new();

        public string? InlineComment { get; set; }

        public ActionDefinition? Definition =>
            FilterCatalog.TryGetAction(Keyword, out var def) ? def : null;

        public bool IsKnown => Definition != null;

        public bool IsColorAction =>
            Keyword.Equals("SetBorderColor", StringComparison.OrdinalIgnoreCase)
            || Keyword.Equals("SetBackgroundColor", StringComparison.OrdinalIgnoreCase)
            || Keyword.Equals("SetTextColor", StringComparison.OrdinalIgnoreCase);

        public bool IsSoundAction =>
            Keyword.Equals("CustomAlertSound", StringComparison.OrdinalIgnoreCase)
            || Keyword.Equals("CustomAlertSoundOptional", StringComparison.OrdinalIgnoreCase);

        public string ArgAt(int index) => index >= 0 && index < Args.Count ? Args[index] : string.Empty;

        public int IntArgAt(int index, int fallback = 0)
        {
            var raw = ArgAt(index);
            return int.TryParse(raw, out var value) ? value : fallback;
        }

        public FilterAction Clone() => new(Keyword)
        {
            Args = new List<string>(Args),
            InlineComment = InlineComment
        };
    }
}
