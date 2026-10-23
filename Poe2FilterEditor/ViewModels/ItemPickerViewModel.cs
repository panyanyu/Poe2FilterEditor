using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Poe2FilterEditor.Infrastructure;
using Poe2FilterEditor.Models;
using Poe2FilterEditor.Services;

namespace Poe2FilterEditor.ViewModels
{
    public enum PickerMode
    {
        Items,
        Classes
    }

    public sealed class ClassFilterOption
    {
        public ClassFilterOption(string? en, string cn)
        {
            En = en;
            Cn = cn;
        }

        public string? En { get; }

        public string Cn { get; }

        public string Display => En == null ? Cn : $"{Cn}";

        public override string ToString() => Display;
    }

    /// <summary>
    /// 一行可选结果（物品或类别）。
    /// </summary>
    public sealed class ItemPickRow : ObservableObject
    {
        private bool _isSelected;

        public ItemPickRow(ItemEntry item)
        {
            Item = item;
            Display = item.NameCn;
            Sub = string.IsNullOrEmpty(item.ClassCn) ? item.NameEn : $"{item.NameEn} · {item.ClassCn}";
            IsUnknown = item.IsUnknown;
        }

        public ItemPickRow(ClassEntry cls)
        {
            Class = cls;
            Display = cls.Cn;
            Sub = cls.En;
        }

        public ItemEntry? Item { get; }

        public ClassEntry? Class { get; }

        public string Display { get; }

        public string Sub { get; }

        public bool IsUnknown { get; }

        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }

    public sealed class ItemPickerViewModel : ObservableObject
    {
        private readonly ItemDatabase _database;
        private string _searchText = string.Empty;
        private ClassFilterOption? _selectedClassFilter;
        private string _newNameEn = string.Empty;
        private string _newNameCn = string.Empty;
        private ClassEntry? _newClass;

        public ItemPickerViewModel(ItemDatabase database, PickerMode mode)
        {
            _database = database;
            Mode = mode;

            ClassFilters.Add(new ClassFilterOption(null, "全部"));
            foreach (var cls in database.Classes)
            {
                ClassFilters.Add(new ClassFilterOption(cls.En, cls.Cn));
            }

            _selectedClassFilter = ClassFilters[0];
            NewClasses = new ObservableCollection<ClassEntry>(database.Classes);

            ToggleAllCommand = new RelayCommand(() => SetAll(true));
            ClearSelectionCommand = new RelayCommand(() => SetAll(false));
            AddCustomCommand = new RelayCommand(AddCustom, () => _newNameEn.Trim().Length > 0);

            Refresh();
        }

        public PickerMode Mode { get; }

        public string Title => Mode == PickerMode.Items ? "添加物品" : "添加物品类别";

        public string Hint => Mode == PickerMode.Items
            ? "按中文名或英文名搜索，勾选后点「添加」写入规则（文件内仍保存英文内部名）。"
            : "按类别中文名搜索，勾选后点「添加」。";

        public bool ShowClassFilter => Mode == PickerMode.Items;

        public ObservableCollection<ItemPickRow> Rows { get; } = new();

        public ObservableCollection<ClassFilterOption> ClassFilters { get; } = new();

        public ObservableCollection<ClassEntry> NewClasses { get; }

        public ICommand ToggleAllCommand { get; }

        public ICommand ClearSelectionCommand { get; }

        public ICommand AddCustomCommand { get; }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    Refresh();
                }
            }
        }

        public ClassFilterOption? SelectedClassFilter
        {
            get => _selectedClassFilter;
            set
            {
                if (SetProperty(ref _selectedClassFilter, value))
                {
                    Refresh();
                }
            }
        }

        public int SelectedCount => Rows.Count(r => r.IsSelected);

        public string SelectionSummary => SelectedCount == 0 ? "尚未选择" : $"已选择 {SelectedCount} 项";

        public string NewNameEn
        {
            get => _newNameEn;
            set
            {
                if (SetProperty(ref _newNameEn, value))
                {
                    (AddCustomCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public string NewNameCn
        {
            get => _newNameCn;
            set => SetProperty(ref _newNameCn, value);
        }

        public ClassEntry? NewClass
        {
            get => _newClass;
            set => SetProperty(ref _newClass, value);
        }

        public string StatusMessage { get; private set; } = string.Empty;

        public void Refresh()
        {
            Rows.Clear();

            if (Mode == PickerMode.Classes)
            {
                foreach (var cls in _database.Classes)
                {
                    if (!string.IsNullOrWhiteSpace(SearchText)
                        && !cls.Cn.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase)
                        && !cls.En.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    AddRow(new ItemPickRow(cls));
                }
            }
            else
            {
                var classEn = SelectedClassFilter?.En;
                foreach (var entry in _database.Search(SearchText, classEn))
                {
                    AddRow(new ItemPickRow(entry));
                }
            }

            RaisePropertyChanged(nameof(SelectedCount), nameof(SelectionSummary));
        }

        private void AddRow(ItemPickRow row)
        {
            row.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(ItemPickRow.IsSelected))
                {
                    RaisePropertyChanged(nameof(SelectedCount), nameof(SelectionSummary));
                }
            };
            Rows.Add(row);
        }

        private void SetAll(bool selected)
        {
            foreach (var row in Rows)
            {
                row.IsSelected = selected;
            }

            RaisePropertyChanged(nameof(SelectedCount), nameof(SelectionSummary));
        }

        public IReadOnlyList<ItemEntry> PickedItems =>
            Rows.Where(r => r.IsSelected && r.Item != null).Select(r => r.Item!).ToList();

        public IReadOnlyList<string> PickedClasses =>
            Rows.Where(r => r.IsSelected && r.Class != null).Select(r => r.Class!.En).ToList();

        /// <summary>未收录名称可以直接补录到用户映射表。</summary>
        public void AddCustom()
        {
            var nameEn = NewNameEn.Trim();
            if (nameEn.Length == 0)
            {
                return;
            }

            var classEn = NewClass?.En ?? SelectedClassFilter?.En ?? string.Empty;
            _database.AddOrUpdateUserItem(nameEn, string.IsNullOrWhiteSpace(NewNameCn) ? nameEn : NewNameCn.Trim(), classEn);
            StatusMessage = $"已补录：{nameEn}";

            NewNameEn = string.Empty;
            NewNameCn = string.Empty;
            Refresh();

            var row = Rows.FirstOrDefault(r => r.Item != null && r.Item.NameEn.Equals(nameEn, StringComparison.OrdinalIgnoreCase));
            if (row != null)
            {
                row.IsSelected = true;
                RaisePropertyChanged(nameof(SelectedCount), nameof(SelectionSummary));
            }

            RaisePropertyChanged(nameof(StatusMessage));
        }
    }
}
