using System;
using System.IO;
using System.Linq;
using System.Threading;
using Poe2FilterEditor.Models;
using Poe2FilterEditor.Services;
using Poe2FilterEditor.ViewModels;

namespace Poe2FilterEditor.Tests
{
    public class RuleViewModelTests
    {
        /// <summary>WPF 画刷等对象需要在 STA 线程创建。</summary>
        private static void RunSta(Action action)
        {
            Exception? failure = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw new InvalidOperationException("STA 线程内断言失败", failure);
            }
        }

        private static (ItemDatabase Database, FilterRule Rule) LoadDivineRule()
        {
            var database = ItemDatabase.Load(AppContext.BaseDirectory,
                Path.Combine(Path.GetTempPath(), "poe2-vm-" + Guid.NewGuid().ToString("N") + ".json"));

            var document = FilterParser.Parse(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "xx-0.4.filter")), null);

            var rule = document.Rules.First(r =>
                r.Conditions.Any(c => c.Keyword == "BaseType" && c.Values.Contains("Divine Orb")));

            return (database, rule);
        }

        [Fact]
        public void 物品标签显示中文名并保留英文内部名()
        {
            RunSta(() =>
            {
                var (database, rule) = LoadDivineRule();
                var vm = new RuleViewModel(rule, database, showChinese: true);

                var chip = vm.ItemChips.Single();
                Assert.Equal("神圣石", chip.Display);
                Assert.Equal("Divine Orb", chip.NameEn);
                Assert.False(chip.IsUnknown);
                Assert.Equal("SHOW", vm.BadgeText);
            });
        }

        [Fact]
        public void 移除物品标签后规则文本同步更新()
        {
            RunSta(() =>
            {
                var (database, rule) = LoadDivineRule();
                var vm = new RuleViewModel(rule, database, showChinese: true);

                vm.RemoveItem("Divine Orb");

                Assert.Empty(vm.ItemChips);
                Assert.Null(rule.FindCondition("BaseType"));
                Assert.DoesNotContain("Divine Orb", vm.RuleText);
                Assert.True(rule.IsModified);
            });
        }

        [Fact]
        public void 添加物品不会重复写入同一个英文名()
        {
            RunSta(() =>
            {
                var (database, rule) = LoadDivineRule();
                var vm = new RuleViewModel(rule, database, showChinese: true);
                var chaos = database.Lookup("Chaos Orb");

                vm.AddItems(new[] { chaos, chaos });

                var values = rule.FindCondition("BaseType")!.Values;
                Assert.Equal(2, values.Count);
                Assert.Contains("Chaos Orb", values);
                Assert.Contains("Divine Orb", values);
            });
        }

        [Fact]
        public void 修改颜色后动作参数被规范化()
        {
            RunSta(() =>
            {
                var (database, rule) = LoadDivineRule();
                var vm = new RuleViewModel(rule, database, showChinese: true);

                vm.BorderColor = new FilterColor(1, 2, 3, 4);

                var action = rule.GetAction("SetBorderColor")!;
                Assert.Equal(new[] { "1", "2", "3", "4" }, action.Args);
                Assert.Contains("SetBorderColor 1 2 3 4", vm.RuleText);
            });
        }

        [Fact]
        public void 停用后规则整块注释且保留全部条件()
        {
            RunSta(() =>
            {
                var (database, rule) = LoadDivineRule();
                var vm = new RuleViewModel(rule, database, showChinese: true);

                vm.SetDisabled(true);

                var lines = vm.RuleText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
                Assert.All(lines, line => Assert.StartsWith("#", line));
                Assert.Contains(lines, line => line.Contains("Divine Orb"));
                Assert.Contains(lines, line => line.Contains("CustomAlertSound"));
            });
        }

        [Fact]
        public void 按传奇添加会写入基底与传奇稀有度()
        {
            RunSta(() =>
            {
                var (database, rule) = LoadDivineRule();
                var vm = new RuleViewModel(rule, database, showChinese: true);

                var headhunter = database.Uniques.First(u => u.NameEn == "Headhunter");
                var waistgate = database.Uniques.First(u => u.NameEn == "Waistgate");

                vm.AddUniques(new[] { headhunter, waistgate });

                Assert.Equal("Unique", rule.FindCondition("Rarity")!.FirstValue);
                var bases = rule.FindCondition("BaseType")!.Values;
                Assert.Contains("Heavy Belt", bases);
                Assert.Single(bases.Where(b => b == "Heavy Belt"));

                Assert.Contains("Rarity Unique", vm.RuleText);
                Assert.Contains("Heavy Belt", vm.RuleText);
                Assert.Contains("猎首", vm.UniqueHitSummary);
                Assert.True(vm.HasUniqueHits);
            });
        }

        [Fact]
        public void 移除基底后传奇命中提示消失()
        {
            RunSta(() =>
            {
                var (database, rule) = LoadDivineRule();
                var vm = new RuleViewModel(rule, database, showChinese: true);

                vm.AddUniques(new[] { database.Uniques.First(u => u.NameEn == "Headhunter") });
                Assert.True(vm.HasUniqueHits);

                vm.RemoveItem("Heavy Belt");
                Assert.False(vm.HasUniqueHits);
                Assert.Equal(string.Empty, vm.UniqueHitSummary);
            });
        }

        [Fact]
        public void 稀有度与数值条件可写入并移除()
        {
            RunSta(() =>
            {
                var (database, rule) = LoadDivineRule();
                var vm = new RuleViewModel(rule, database, showChinese: true);

                vm.RarityValue = "Unique";
                Assert.Equal("Unique", rule.FindCondition("Rarity")!.FirstValue);
                Assert.Contains("传奇", vm.Subline);

                vm.RarityValue = string.Empty;
                Assert.Null(rule.FindCondition("Rarity"));

                var itemLevel = vm.Numerics.Single(n => n.Keyword == "ItemLevel");
                itemLevel.IsEnabled = true;
                itemLevel.ValueText = "82";

                var condition = rule.FindCondition("ItemLevel")!;
                Assert.Equal("82", condition.FirstValue);
                Assert.Equal(CompareOp.GreaterOrEqual, condition.Op);

                itemLevel.IsEnabled = false;
                Assert.Null(rule.FindCondition("ItemLevel"));
            });
        }
    }
}
