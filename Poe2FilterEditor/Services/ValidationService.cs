using System;
using System.Collections.Generic;
using System.Linq;
using Poe2FilterEditor.Models;

namespace Poe2FilterEditor.Services
{
    /// <summary>
    /// 过滤器语法与内容校验（宽容模式：只提示，不阻断保存）。
    /// </summary>
    public static class ValidationService
    {
        public static List<ValidationIssue> Validate(FilterDocument document, ItemDatabase database, string? soundRoot)
        {
            var issues = new List<ValidationIssue>();
            var signatures = new Dictionary<string, FilterRule>(StringComparer.Ordinal);

            foreach (var rule in document.Rules)
            {
                ValidateRule(rule, document, database, soundRoot, issues);

                var signature = rule.Signature();
                if (signatures.TryGetValue(signature, out var first))
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Info,
                        $"与第 {first.LineNumber} 行的规则条件完全相同，可能被前面的规则完全覆盖。", rule));
                }
                else
                {
                    signatures[signature] = rule;
                }
            }

            return issues
                .OrderBy(i => i.Severity == IssueSeverity.Error ? 0 : i.Severity == IssueSeverity.Warning ? 1 : 2)
                .ThenBy(i => i.LineNumber)
                .ToList();
        }

        private static void ValidateRule(FilterRule rule, FilterDocument document, ItemDatabase database,
            string? soundRoot, List<ValidationIssue> issues)
        {
            var conditionCount = rule.Conditions.Count(c => c.Keyword is not ("Class" or "BaseType") || c.Values.Count > 0);
            if (conditionCount == 0 && rule.Conditions.Count == 0 && rule.Actions.Count == 0)
            {
                issues.Add(new ValidationIssue(IssueSeverity.Warning, "规则没有任何条件与动作，将被客户端忽略。", rule));
            }

            foreach (var condition in rule.Conditions)
            {
                if (!condition.IsKnown)
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, $"未识别的条件「{condition.Keyword}」，将原样保留。", rule));
                }

                if (condition.Op != CompareOp.Implicit)
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Info,
                        $"条件「{FilterCatalog.ChineseOfCondition(condition.Keyword)}」使用了非标准比较符 {condition.OpSymbol}，建议改为隐式写法。", rule));
                }

                switch (condition.Keyword.ToUpperInvariant())
                {
                    case "BASETYPE":
                    {
                        foreach (var value in condition.Values)
                        {
                            var entry = database.Lookup(value);
                            if (entry.IsUnknown)
                            {
                                issues.Add(new ValidationIssue(IssueSeverity.Info,
                                    $"物品名「{value}」不在内置国服映射表中，界面会显示英文原名。", rule));
                            }

                            if (entry.ClassEn.Length > 0 && !entry.ClassEn.Equals(GetClassValue(rule), StringComparison.OrdinalIgnoreCase)
                                && GetClassValue(rule).Length > 0)
                            {
                                issues.Add(new ValidationIssue(IssueSeverity.Warning,
                                    $"「{entry.NameCn}」属于类别 {database.ChineseClassName(entry.ClassEn)}，与本规则中的类别不一致。", rule));
                            }
                        }

                        break;
                    }

                    case "CLASS":
                    {
                        foreach (var value in condition.Values)
                        {
                            if (database.ChineseClassName(value) == value)
                            {
                                issues.Add(new ValidationIssue(IssueSeverity.Info,
                                    $"类别「{value}」暂无中文译名。", rule));
                            }
                        }

                        break;
                    }

                    case "RARITY":
                    {
                        foreach (var value in condition.Values)
                        {
                            if (!FilterCatalog.Rarities.Contains(value, StringComparer.OrdinalIgnoreCase))
                            {
                                issues.Add(new ValidationIssue(IssueSeverity.Warning, $"稀有度「{value}」不是有效值。", rule));
                            }
                        }

                        break;
                    }

                    case "ITEMLEVEL":
                    case "DROPLEVEL":
                    case "AREALEVEL":
                    case "GEMLEVEL":
                    case "STACKSIZE":
                    case "QUALITY":
                    case "WAYSTONETIER":
                    case "MAPTIER":
                    case "LINKEDSOCKETS":
                    case "HEIGHT":
                    case "WIDTH":
                    {
                        if (!condition.Values.Any(v => int.TryParse(v, out _)))
                        {
                            issues.Add(new ValidationIssue(IssueSeverity.Warning,
                                $"条件「{FilterCatalog.ChineseOfCondition(condition.Keyword)}」需要一个数字。", rule));
                        }

                        break;
                    }
                }
            }

            foreach (var action in rule.Actions)
            {
                if (!action.IsKnown)
                {
                    issues.Add(new ValidationIssue(IssueSeverity.Warning, $"未识别的动作「{action.Keyword}」，将原样保留。", rule));
                    continue;
                }

                if (action.IsColorAction)
                {
                    if (action.Args.Count != 4 || action.Args.Any(a => !IsByte(a)))
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Error,
                            $"「{action.Definition!.ChineseName}」需要 4 个 0-255 的数值（红 绿 蓝 透明度）。", rule));
                    }
                }
                else if (action.Keyword.Equals("SetFontSize", StringComparison.OrdinalIgnoreCase))
                {
                    if (action.Args.Count != 1 || !int.TryParse(action.Args[0], out var size) || size < 1 || size > 45)
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Error, "字体大小必须是 1-45 之间的整数。", rule));
                    }
                }
                else if (action.Keyword.Equals("PlayEffect", StringComparison.OrdinalIgnoreCase))
                {
                    if (action.Args.Count == 0 || !FilterCatalog.EffectColors.Contains(action.Args[0], StringComparer.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Error, "掉落光柱需要一个有效颜色，例如 Red。", rule));
                    }
                }
                else if (action.Keyword.Equals("MinimapIcon", StringComparison.OrdinalIgnoreCase))
                {
                    if (action.Args.Count < 3)
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Error, "小地图图标需要三个参数：大小 颜色 形状。", rule));
                    }
                    else
                    {
                        if (!FilterCatalog.IconSizes.Contains(action.Args[0]))
                        {
                            issues.Add(new ValidationIssue(IssueSeverity.Warning, "小地图图标大小只能是 0、1、2。", rule));
                        }

                        if (!FilterCatalog.IconColors.Contains(action.Args[1], StringComparer.OrdinalIgnoreCase))
                        {
                            issues.Add(new ValidationIssue(IssueSeverity.Warning, "小地图图标颜色无效。", rule));
                        }

                        if (!FilterCatalog.IconShapes.Contains(action.Args[2], StringComparer.OrdinalIgnoreCase))
                        {
                            issues.Add(new ValidationIssue(IssueSeverity.Warning, "小地图图标形状无效。", rule));
                        }
                    }
                }
                else if (action.IsSoundAction)
                {
                    if (action.Args.Count == 0 || string.IsNullOrWhiteSpace(action.Args[0]))
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Error, "自定义音效缺少文件路径。", rule));
                        continue;
                    }

                    if (action.Args.Count > 1 && (!int.TryParse(action.Args[1], out var volume) || volume < 0 || volume > 400))
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Warning, "音效音量建议在 0-300 之间。", rule));
                    }

                    if (!SoundService.HasKnownExtension(action.Args[0]))
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Warning, "音效文件建议使用 .mp3 / .wav / .ogg 格式。", rule));
                    }

                    if (!SoundService.Exists(action.Args[0], document.FilePath, soundRoot))
                    {
                        issues.Add(new ValidationIssue(IssueSeverity.Info,
                            $"音效文件「{action.Args[0]}」在本地未找到，游戏内可能无法播放。", rule));
                    }
                }
            }
        }

        private static string GetClassValue(FilterRule rule) => rule.FindCondition("Class")?.FirstValue ?? string.Empty;

        private static bool IsByte(string value) => byte.TryParse(value, out _);
    }
}
