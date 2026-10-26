using System;
using System.IO;
using System.Linq;
using Poe2FilterEditor.Models;
using Poe2FilterEditor.Services;
using Poe2FilterEditor.ViewModels;

namespace Poe2FilterEditor.Tests
{
    public class ItemPickerTests
    {
        private static ItemDatabase CreateDatabase() =>
            ItemDatabase.Load(AppContext.BaseDirectory,
                Path.Combine(Path.GetTempPath(), "poe2-picker-" + Guid.NewGuid().ToString("N") + ".json"));

        [Fact]
        public void 传奇选择器可按中文名搜索并多选()
        {
            var vm = new ItemPickerViewModel(CreateDatabase(), PickerMode.Uniques);

            Assert.Equal("添加传奇物品", vm.Title);
            Assert.False(vm.ShowCustomEntry);
            Assert.True(vm.Rows.Count > 400);

            vm.SearchText = "猎首";
            var row = Assert.Single(vm.Rows);
            Assert.True(row.IsUnique);
            Assert.Equal("猎首", row.Display);

            row.IsSelected = true;
            var picked = Assert.Single(vm.PickedUniques);
            Assert.Equal("Heavy Belt", picked.BaseEn);
        }

        [Fact]
        public void 传奇选择器可按类别筛选()
        {
            var vm = new ItemPickerViewModel(CreateDatabase(), PickerMode.Uniques);
            vm.SelectedClassFilter = vm.ClassFilters.First(c => c.En == "Belts");

            Assert.All(vm.Rows, r => Assert.Equal("Belts", r.Unique!.ClassEn));
            Assert.Contains(vm.Rows, r => r.Unique!.NameEn == "Headhunter");
        }

        [Fact]
        public void 物品选择器仍然可以补录未收录名称()
        {
            var database = CreateDatabase();
            var before = database.ItemCount;
            var vm = new ItemPickerViewModel(database, PickerMode.Items)
            {
                NewNameEn = "Custom Test Orb",
                NewNameCn = "测试宝珠"
            };

            vm.AddCustom();

            var entry = Assert.Single(database.Search("Custom Test Orb"));
            Assert.Equal("测试宝珠", entry.NameCn);
            Assert.Equal(before + 1, database.ItemCount);

            if (File.Exists(database.UserFilePath))
            {
                File.Delete(database.UserFilePath!);
            }
        }
    }
}
