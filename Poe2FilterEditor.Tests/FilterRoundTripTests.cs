using System;
using System.IO;
using System.Linq;
using Poe2FilterEditor.Models;
using Poe2FilterEditor.Services;

namespace Poe2FilterEditor.Tests
{
    public class FilterRoundTripTests
    {
        private static string SamplePath => Path.Combine(AppContext.BaseDirectory, "xx-0.4.filter");

        [Fact]
        public void 样例过滤器解析后原样写回()
        {
            var original = File.ReadAllText(SamplePath);
            var document = FilterParser.Parse(original, SamplePath);

            var written = FilterWriter.Write(document);

            Assert.Equal(original.Replace("\r\n", "\n"), written.Replace("\r\n", "\n"));
        }

        [Fact]
        public void 解析出全部规则且没有无法识别的顶层行()
        {
            var document = FilterParser.Parse(File.ReadAllText(SamplePath), SamplePath);

            Assert.True(document.RuleCount > 40, $"规则数偏少：{document.RuleCount}");
            Assert.Empty(document.Nodes.OfType<RawNode>());
        }

        [Fact]
        public void 规则内部条件与动作都被识别()
        {
            var document = FilterParser.Parse(File.ReadAllText(SamplePath), SamplePath);
            var divine = document.Rules.First(r => r.Conditions.Any(c =>
                c.Keyword == "BaseType" && c.Values.Contains("Divine Orb")));

            Assert.True(divine.IsShow);
            Assert.Equal("Currency", divine.FindCondition("Class")!.FirstValue);
            Assert.Equal(CompareOp.Equal, divine.FindCondition("BaseType")!.Op);
            Assert.Equal("Divine Orb", divine.FindCondition("BaseType")!.FirstValue);

            var border = divine.GetAction("SetBorderColor");
            Assert.NotNull(border);
            Assert.Equal(new[] { "255", "120", "0", "255" }, border!.Args);

            var sound = divine.GetAction("CustomAlertSound");
            Assert.NotNull(sound);
            Assert.Equal("sounds/神圣石.mp3", sound!.Args[0]);
            Assert.Equal("300", sound.Args[1]);

            Assert.Equal("金边", border.InlineComment);
            Assert.Empty(divine.ExtraLines);
        }

        [Fact]
        public void 多值类别条件可以正确拆分()
        {
            var document = FilterParser.Parse(File.ReadAllText(SamplePath), SamplePath);
            var rule = document.Rules.First(r =>
                r.FindCondition("Class")?.Values.Count == 7 && r.FindCondition("Class")!.Values.Contains("Crossbows"));

            Assert.Contains("One Hand Axes", rule.FindCondition("Class")!.Values);
        }

        [Fact]
        public void 修改规则后仍保留无法识别的条件()
        {
            const string text = "Show\n    Class \"Currency\"\n    HasExplicitMod \"foo\" \"bar\"\n    SetTextColor 1 2 3 4\n";
            var document = FilterParser.Parse(text, null);
            var rule = document.Rules.Single();

            rule.IsShow = false;
            rule.MarkModified();

            var written = FilterWriter.Write(document);

            Assert.Contains("Hide", written);
            Assert.Contains("HasExplicitMod \"foo\" \"bar\"", written);
            Assert.Contains("SetTextColor 1 2 3 4", written);
        }

        [Fact]
        public void 归一化可以去掉非标准比较符()
        {
            const string text = "Show\n    Class == \"Currency\"\n    BaseType >= \"Divine Orb\"\n";
            var document = FilterParser.Parse(text, null);

            var normalized = FilterWriter.Write(document, normalizeOperators: true);

            Assert.DoesNotContain("==", normalized);
            Assert.DoesNotContain(">=", normalized);
            Assert.Contains("Class \"Currency\"", normalized);
        }

        [Fact]
        public void 停用规则写回为整块注释()
        {
            const string text = "Show\n    Class \"Currency\"\n    SetFontSize 45\n";
            var document = FilterParser.Parse(text, null);
            var rule = document.Rules.Single();

            rule.IsDisabled = true;
            var written = FilterWriter.Write(document);
            var lines = written.Split('\n', StringSplitOptions.RemoveEmptyEntries);

            Assert.All(lines, line => Assert.StartsWith("#", line));

            var reparsed = FilterParser.Parse(written, null);
            Assert.Single(reparsed.Rules);
            Assert.True(reparsed.Rules.Single().IsDisabled);
            Assert.Equal("Currency", reparsed.Rules.Single().FindCondition("Class")!.FirstValue);
        }

        [Fact]
        public void 空行与行尾换行保持稳定()
        {
            const string text = "Show\n    Class \"Currency\"\n\n\nHide\n    Class \"Belts\"\n";
            var document = FilterParser.Parse(text, null);

            Assert.True(document.EndsWithNewLine);
            Assert.Equal(text.Replace("\r\n", "\n"), FilterWriter.Write(document).Replace("\r\n", "\n"));
        }

        [Fact]
        public void 无法识别的行降级为原始行不丢失()
        {
            const string text = "Show\n    Class \"Currency\"\n    SomeUnknownDirective 1 2 3\n";
            var document = FilterParser.Parse(text, null);
            var rule = document.Rules.Single();

            Assert.Contains("SomeUnknownDirective 1 2 3", rule.ExtraLines);
            Assert.Contains("SomeUnknownDirective 1 2 3", FilterWriter.Write(document));
        }

        [Fact]
        public void 保存后重新载入修改仍然存在且生成备份()
        {
            var temp = Path.Combine(Path.GetTempPath(), "poe2-save-" + Guid.NewGuid().ToString("N") + ".filter");
            File.Copy(SamplePath, temp, true);

            try
            {
                var document = FilterFileService.Load(temp);
                var rule = document.Rules.First(r => r.Conditions.Any(c =>
                    c.Keyword == "BaseType" && c.Values.Contains("Divine Orb")));

                rule.SetConditionValue("Rarity", "Unique");
                FilterFileService.Save(document, temp);

                var reloaded = FilterFileService.Load(temp);
                var reloadedRule = reloaded.Rules.First(r => r.Conditions.Any(c => c.Values.Contains("Divine Orb")));

                Assert.Equal("Unique", reloadedRule.FindCondition("Rarity")!.FirstValue);
                Assert.Equal(document.RuleCount, reloaded.RuleCount);
                Assert.True(File.Exists(temp + ".bak"));
                Assert.False(document.IsDirty);
                Assert.False(reloadedRule.IsModified);
            }
            finally
            {
                if (File.Exists(temp))
                {
                    File.Delete(temp);
                }

                if (File.Exists(temp + ".bak"))
                {
                    File.Delete(temp + ".bak");
                }
            }
        }

        [Fact]
        public void 颜色解析与格式化()
        {
            Assert.True(FilterColor.TryParse("255 120 0 255", out var color));
            Assert.Equal((byte)255, color.R);
            Assert.Equal((byte)120, color.G);
            Assert.Equal((byte)0, color.B);
            Assert.Equal("#FF7800", color.ToHexText());
            Assert.Equal("255 120 0 255", color.ToRgbText());
        }
    }
}
