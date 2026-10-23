using System;
using System.Globalization;
using System.Windows.Media;

namespace Poe2FilterEditor.Models
{
    /// <summary>
    /// 过滤器颜色（RGBA，0-255）。
    /// </summary>
    public readonly struct FilterColor : IEquatable<FilterColor>
    {
        public FilterColor(byte r, byte g, byte b, byte a = 255)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public byte R { get; }

        public byte G { get; }

        public byte B { get; }

        public byte A { get; }

        public static FilterColor White => new(255, 255, 255, 255);

        public static FilterColor Transparent => new(0, 0, 0, 0);

        public Color ToColor() => Color.FromArgb(A, R, G, B);

        public Brush ToBrush()
        {
            var brush = new SolidColorBrush(ToColor());
            brush.Freeze();
            return brush;
        }

        public string ToRgbText() => $"{R} {G} {B} {A}";

        public string ToHexText() => $"#{R:X2}{G:X2}{B:X2}";

        public static FilterColor FromColor(Color color) => new(color.R, color.G, color.B, color.A);

        public static FilterColor Parse(string r, string g, string b, string a = "255")
        {
            return new FilterColor(ToByte(r), ToByte(g), ToByte(b), ToByte(a));
        }

        private static byte ToByte(string value)
        {
            return byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : (byte)0;
        }

        /// <summary>解析 #RRGGBB / #AARRGGBB / "255 0 0 255"。</summary>
        public static bool TryParse(string? text, out FilterColor color)
        {
            color = White;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim();
            if (text.StartsWith("#", StringComparison.Ordinal))
            {
                try
                {
                    var converted = (Color)ColorConverter.ConvertFromString(text)!;
                    color = FromColor(converted);
                    return true;
                }
                catch
                {
                    return false;
                }
            }

            var parts = text.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 3)
            {
                return false;
            }

            color = Parse(parts[0], parts[1], parts[2], parts.Length > 3 ? parts[3] : "255");
            return true;
        }

        public bool Equals(FilterColor other) => R == other.R && G == other.G && B == other.B && A == other.A;

        public override bool Equals(object? obj) => obj is FilterColor other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(R, G, B, A);

        public override string ToString() => ToRgbText();

        public static bool operator ==(FilterColor left, FilterColor right) => left.Equals(right);

        public static bool operator !=(FilterColor left, FilterColor right) => !left.Equals(right);
    }
}
