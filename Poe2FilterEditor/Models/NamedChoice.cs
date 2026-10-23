namespace Poe2FilterEditor.Models
{
    /// <summary>下拉框用的「值 + 中文文本」选项。</summary>
    public sealed class NamedChoice
    {
        public NamedChoice(string value, string text)
        {
            Value = value;
            Text = text;
        }

        public string Value { get; }

        public string Text { get; }

        public override string ToString() => Text;

        public static NamedChoice[] Rarities => new[]
        {
            new NamedChoice(string.Empty, "不限"),
            new NamedChoice("Normal", "普通"),
            new NamedChoice("Magic", "魔法"),
            new NamedChoice("Rare", "稀有"),
            new NamedChoice("Unique", "传奇")
        };

        public static NamedChoice[] Booleans => new[]
        {
            new NamedChoice(string.Empty, "不限"),
            new NamedChoice("True", "是"),
            new NamedChoice("False", "否")
        };

        public static NamedChoice[] Operators => new[]
        {
            new NamedChoice(">=", ">="),
            new NamedChoice("<=", "<="),
            new NamedChoice("==", "="),
            new NamedChoice(">", ">"),
            new NamedChoice("<", "<")
        };

        public static NamedChoice[] EffectColors => BuildColors();
        public static NamedChoice[] IconColors => BuildColors();
        public static NamedChoice[] IconSizes => new[]
        {
            new NamedChoice("0", "0 - 大"),
            new NamedChoice("1", "1 - 中"),
            new NamedChoice("2", "2 - 小")
        };

        public static NamedChoice[] IconShapes
        {
            get
            {
                var shapes = FilterCatalog.IconShapes;
                var result = new NamedChoice[shapes.Length];
                for (var i = 0; i < shapes.Length; i++)
                {
                    result[i] = new NamedChoice(shapes[i], FilterCatalog.ShapeToChinese(shapes[i]));
                }

                return result;
            }
        }

        private static NamedChoice[] BuildColors()
        {
            var colors = FilterCatalog.EffectColors;
            var result = new NamedChoice[colors.Length];
            for (var i = 0; i < colors.Length; i++)
            {
                result[i] = new NamedChoice(colors[i], FilterCatalog.ColorToChinese(colors[i]));
            }

            return result;
        }
    }
}
