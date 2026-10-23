using System;
using System.Collections.Generic;
using System.Linq;

namespace Poe2FilterEditor.Models
{
    public enum ConditionValueKind
    {
        Text,
        Number,
        Boolean,
        Enumeration
    }

    /// <summary>
    /// 条件关键字（Class / BaseType / Rarity ...）的中文元数据。
    /// </summary>
    public sealed class ConditionDefinition
    {
        public ConditionDefinition(string keyword, string chineseName, ConditionValueKind kind,
            bool isMultiValue = false, string[]? allowedValues = null, string description = "")
        {
            Keyword = keyword;
            ChineseName = chineseName;
            Kind = kind;
            IsMultiValue = isMultiValue;
            AllowedValues = allowedValues ?? Array.Empty<string>();
            Description = description;
        }

        public string Keyword { get; }

        public string ChineseName { get; }

        public ConditionValueKind Kind { get; }

        public bool IsMultiValue { get; }

        public string[] AllowedValues { get; }

        public string Description { get; }

        /// <summary>
        /// 界面可直接编辑的条件（Class/BaseType 走物品选择器，数值/布尔条件走专用编辑器）。
        /// </summary>
        public bool IsNumeric => Kind == ConditionValueKind.Number;
    }

    /// <summary>
    /// 动作关键字的中文元数据。
    /// </summary>
    public sealed class ActionDefinition
    {
        public ActionDefinition(string keyword, string chineseName, string hint)
        {
            Keyword = keyword;
            ChineseName = chineseName;
            Hint = hint;
        }

        public string Keyword { get; }

        public string ChineseName { get; }

        public string Hint { get; }
    }

    public static class FilterCatalog
    {
        public static readonly string[] Rarities = { "Normal", "Magic", "Rare", "Unique" };

        public static readonly string[] EffectColors =
        {
            "Red", "Green", "Blue", "Brown", "White", "Yellow", "Cyan", "Grey", "Orange", "Pink", "Purple"
        };

        public static readonly string[] IconColors =
        {
            "Red", "Green", "Blue", "Brown", "White", "Yellow", "Cyan", "Grey", "Orange", "Pink", "Purple"
        };

        public static readonly string[] IconShapes =
        {
            "Circle", "Diamond", "Hexagon", "Square", "Star", "Triangle", "Cross", "Moon", "Pentagon"
        };

        public static readonly string[] IconSizes = { "0", "1", "2" };

        public static readonly IReadOnlyList<ConditionDefinition> Conditions = new List<ConditionDefinition>
        {
            new("Class", "物品类别", ConditionValueKind.Text, true, null, "对应游戏内的物品类别，例如 Currency、Belts。"),
            new("BaseType", "物品名称", ConditionValueKind.Text, true, null, "物品的英文内部名，界面显示为国服中文名。"),
            new("Rarity", "稀有度", ConditionValueKind.Enumeration, false, Rarities, "普通 / 魔法 / 稀有 / 传奇。"),
            new("ItemLevel", "物品等级", ConditionValueKind.Number, false, null, "物品自身的等级。"),
            new("DropLevel", "掉落等级", ConditionValueKind.Number, false, null, "物品的掉落等级需求。"),
            new("AreaLevel", "区域等级", ConditionValueKind.Number, false, null, "所在区域等级。"),
            new("GemLevel", "宝石等级", ConditionValueKind.Number, false, null, "未切割宝石的等级。"),
            new("StackSize", "堆叠数量", ConditionValueKind.Number, false, null, "可堆叠物品的数量。"),
            new("Quality", "品质", ConditionValueKind.Number, false, null, "物品品质百分比。"),
            new("WaystoneTier", "引路石阶级", ConditionValueKind.Number, false, null, "引路石的阶级。"),
            new("MapTier", "地图阶级", ConditionValueKind.Number, false, null, "地图阶级。"),
            new("LinkedSockets", "已连接插槽", ConditionValueKind.Number, false, null, "最大连接插槽数。"),
            new("Height", "高度", ConditionValueKind.Number, false, null, "物品占用的高度格数。"),
            new("Width", "宽度", ConditionValueKind.Number, false, null, "物品占用的宽度格数。"),
            new("Corrupted", "已腐化", ConditionValueKind.Boolean, false, null, "物品是否被腐化。"),
            new("Identified", "已鉴定", ConditionValueKind.Boolean, false, null, "物品是否已鉴定。"),
            new("Mirrored", "已镜像", ConditionValueKind.Boolean, false, null, "物品是否被镜像。"),
            new("AnyEnchantment", "任意附魔", ConditionValueKind.Boolean, false, null, "物品是否带有附魔。"),
            new("HasExplicitMod", "固定词缀", ConditionValueKind.Text, true, null, "匹配特定词缀文本。"),
            new("HasImplicitMod", "内建词缀", ConditionValueKind.Boolean, false, null, "是否带有内建词缀。"),
            new("HasEnchantment", "附魔词缀", ConditionValueKind.Text, true, null, "匹配特定附魔文本。"),
            new("SocketGroup", "插槽连接", ConditionValueKind.Text, false, null, "匹配插槽颜色组合，例如 R-G-B。"),
            new("HasInfluence", "势力影响", ConditionValueKind.Text, true, null, "匹配势力影响类型。"),
            new("ZoneType", "区域类型", ConditionValueKind.Text, false, null, "匹配区域类型。"),
            new("BlightedMap", "菌潮地图", ConditionValueKind.Boolean, false, null, "是否为菌潮地图。"),
            new("UberBlightedMap", "高级菌潮地图", ConditionValueKind.Boolean, false, null, "是否为高级菌潮地图。"),
            new("TransfiguredGem", "变体宝石", ConditionValueKind.Boolean, false, null, "是否为变体宝石。"),
            new("IsVaalUnique", "瓦尔传奇", ConditionValueKind.Boolean, false, null, "是否为瓦尔传奇物品。"),
            new("HasCruciblePassiveTree", "熔炉天赋树", ConditionValueKind.Boolean, false, null, "是否带有熔炉天赋树。")
        };

        public static readonly IReadOnlyList<ActionDefinition> Actions = new List<ActionDefinition>
        {
            new("SetBorderColor", "边框颜色", "四个数字：红 绿 蓝 透明度"),
            new("SetBackgroundColor", "背景颜色", "四个数字：红 绿 蓝 透明度"),
            new("SetTextColor", "文字颜色", "四个数字：红 绿 蓝 透明度"),
            new("SetFontSize", "字体大小", "1 ~ 45"),
            new("PlayEffect", "掉落光柱", "颜色，可附加 Temp"),
            new("MinimapIcon", "小地图图标", "大小 0-2、颜色、形状"),
            new("PlayAlertSound", "提示音", "音效 ID 或名称，可附加音量"),
            new("CustomAlertSound", "自定义音效", "音效文件路径，可附加音量"),
            new("CustomAlertSoundOptional", "自定义音效（可选）", "音效文件缺失时静默"),
            new("DisableDropSound", "关闭掉落音", "无参数"),
            new("EnableDropSound", "开启掉落音", "无参数"),
            new("Continue", "继续匹配", "继续执行后续规则")
        };

        private static readonly Dictionary<string, ConditionDefinition> ConditionMap =
            Conditions.ToDictionary(c => c.Keyword, StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, ActionDefinition> ActionMap =
            Actions.ToDictionary(a => a.Keyword, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 需要以「物品中文名」编辑的条件。
        /// </summary>
        public static readonly HashSet<string> ItemConditions =
            new(StringComparer.OrdinalIgnoreCase) { "BaseType", "Class" };

        public static bool TryGetCondition(string keyword, out ConditionDefinition definition) =>
            ConditionMap.TryGetValue(keyword, out definition!);

        public static bool TryGetAction(string keyword, out ActionDefinition definition) =>
            ActionMap.TryGetValue(keyword, out definition!);

        public static bool IsCondition(string keyword) => ConditionMap.ContainsKey(keyword);

        public static bool IsAction(string keyword) => ActionMap.ContainsKey(keyword);

        public static string ChineseOfCondition(string keyword) =>
            ConditionMap.TryGetValue(keyword, out var def) ? def.ChineseName : keyword;

        public static string ChineseOfAction(string keyword) =>
            ActionMap.TryGetValue(keyword, out var def) ? def.ChineseName : keyword;

        public static string RarityToChinese(string value) => value.ToLowerInvariant() switch
        {
            "normal" => "普通",
            "magic" => "魔法",
            "rare" => "稀有",
            "unique" => "传奇",
            _ => value
        };

        public static string ChineseToRarity(string chinese)
        {
            foreach (var rarity in Rarities)
            {
                if (RarityToChinese(rarity) == chinese)
                {
                    return rarity;
                }
            }

            return chinese;
        }

        /// <summary>
        /// 颜色关键字的中文名（用于图标/光柱下拉）。
        /// </summary>
        public static string ColorToChinese(string color) => color.ToLowerInvariant() switch
        {
            "red" => "红",
            "green" => "绿",
            "blue" => "蓝",
            "brown" => "棕",
            "white" => "白",
            "yellow" => "黄",
            "cyan" => "青",
            "grey" => "灰",
            "orange" => "橙",
            "pink" => "粉",
            "purple" => "紫",
            _ => color
        };

        public static string ShapeToChinese(string shape) => shape.ToLowerInvariant() switch
        {
            "circle" => "圆形",
            "diamond" => "菱形",
            "hexagon" => "六边形",
            "square" => "方形",
            "star" => "星形",
            "triangle" => "三角",
            "cross" => "十字",
            "moon" => "月牙",
            "pentagon" => "五边形",
            _ => shape
        };
    }
}
