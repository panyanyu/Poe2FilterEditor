using System;
using System.IO;
using System.Linq;
using Poe2FilterEditor.Models;
using Poe2FilterEditor.Services;

namespace Poe2FilterEditor.Tests
{
    public class ItemDatabaseTests
    {
        private static ItemDatabase CreateDatabase()
        {
            var userFile = Path.Combine(Path.GetTempPath(), "poe2-items-" + Guid.NewGuid().ToString("N") + ".json");
            return ItemDatabase.Load(AppContext.BaseDirectory, userFile);
        }

        [Fact]
        public void 内置映射表中英双向可查()
        {
            var database = CreateDatabase();

            Assert.True(database.ItemCount > 100);

            var divine = database.Lookup("Divine Orb");
            Assert.False(divine.IsUnknown);
            Assert.Equal("神圣石", divine.NameCn);
            Assert.Equal("通货", divine.ClassCn);

            var byChinese = database.TryLookupChinese("混沌石");
            Assert.NotNull(byChinese);
            Assert.Equal("Chaos Orb", byChinese!.NameEn);

            Assert.Equal("通货", database.ChineseClassName("Currency"));
            Assert.Equal("Currency", database.EnglishClassName("通货"));
        }

        [Fact]
        public void 未收录物品返回占位条目且不丢失原名()
        {
            var database = CreateDatabase();

            var unknown = database.Lookup("Some Unknown Base");
            Assert.True(unknown.IsUnknown);
            Assert.Equal("Some Unknown Base", unknown.NameEn);
        }

        [Fact]
        public void 样例过滤器中的物品名大多能翻译为中文()
        {
            var database = CreateDatabase();
            var document = FilterParser.Parse(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "xx-0.4.filter")), null);

            var names = document.Rules
                .SelectMany(r => r.Conditions.Where(c => c.Keyword == "BaseType"))
                .SelectMany(c => c.Values)
                .Distinct()
                .ToList();

            var translated = names.Count(n => !database.Lookup(n).IsUnknown);

            Assert.True(translated >= names.Count * 0.9,
                $"仅翻译了 {translated}/{names.Count} 个物品名");
        }

        [Fact]
        public void 搜索支持中文英文与类别()
        {
            var database = CreateDatabase();

            Assert.Contains(database.Search("神圣"), e => e.NameEn == "Divine Orb");
            Assert.Contains(database.Search("Divine"), e => e.NameEn == "Divine Orb");
            Assert.All(database.Search(null, "Omen"), e => Assert.Equal("Omen", e.ClassEn));
        }

        [Fact]
        public void 补录条目写入用户文件并覆盖内置条目()
        {
            var database = CreateDatabase();
            var userFile = database.UserFilePath!;

            database.AddOrUpdateUserItem("Divine Orb", "神圣宝珠", "Currency");

            Assert.Equal("神圣宝珠", database.Lookup("Divine Orb").NameCn);
            Assert.True(File.Exists(userFile));

            var reloaded = ItemDatabase.Load(AppContext.BaseDirectory, userFile);
            Assert.Equal("神圣宝珠", reloaded.Lookup("Divine Orb").NameCn);

            File.Delete(userFile);
        }

        [Fact]
        public void 校验服务能发现非法字体大小与非标准比较符()
        {
            var database = CreateDatabase();
            const string text = "Show\n    Class == \"Currency\"\n    SetFontSize 99\n    SetBorderColor 300 0 0 255\n";
            var document = FilterParser.Parse(text, null);

            var issues = ValidationService.Validate(document, database, null);

            Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("字体大小"));
            Assert.Contains(issues, i => i.Severity == IssueSeverity.Error && i.Message.Contains("边框颜色"));
            Assert.Contains(issues, i => i.Severity == IssueSeverity.Info && i.Message.Contains("=="));
        }
    }
}
