using Poe2FilterEditor.Infrastructure;
using Poe2FilterEditor.Models;

namespace Poe2FilterEditor.ViewModels
{
    /// <summary>
    /// 物品 / 类别标签：界面显示中文名，底层保存英文内部名。
    /// </summary>
    public sealed class ItemChipViewModel : ObservableObject
    {
        public ItemChipViewModel(ItemEntry entry, bool showChinese)
        {
            Entry = entry;
            ShowChinese = showChinese;
        }

        public ItemEntry Entry { get; }

        public string NameEn => Entry.NameEn;

        public bool ShowChinese { get; set; }

        public string Display => ShowChinese ? Entry.NameCn : Entry.NameEn;

        public string EnglishName => Entry.NameEn;

        public bool IsUnknown => Entry.IsUnknown;

        public string ToolTipText => string.IsNullOrEmpty(Entry.ClassCn)
            ? $"{Entry.NameCn}（{Entry.NameEn}）"
            : $"{Entry.NameCn}（{Entry.NameEn}）\n类别：{Entry.ClassCn}（{Entry.ClassEn}）";

        public void RefreshDisplay()
        {
            RaisePropertyChanged(nameof(Display));
        }
    }
}
