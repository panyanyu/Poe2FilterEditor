using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using Poe2FilterEditor.Infrastructure;
using Poe2FilterEditor.Models;
using Poe2FilterEditor.Services;

namespace Poe2FilterEditor.ViewModels
{
    /// <summary>
    /// 单条规则的视图模型：中文摘要、条件（物品中文标签 / 数值 / 布尔）、外观与音效。
    /// </summary>
    public sealed partial class RuleViewModel : PaneEntry
    {
        private static readonly string[] NumericKeywords =
        {
            "ItemLevel", "DropLevel", "AreaLevel", "GemLevel", "StackSize", "Quality", "WaystoneTier", "LinkedSockets", "Height", "Width"
        };

        private static readonly string[] BooleanKeywords =
        {
            "Corrupted", "Identified", "Mirrored", "AnyEnchantment"
        };

        private static readonly HashSet<string> ManagedKeywords = new(StringComparer.OrdinalIgnoreCase)
        {
            "Class", "BaseType", "Rarity"
        };

        private readonly ItemDatabase _database;
        private bool _updating;

        public RuleViewModel(FilterRule model, ItemDatabase database, bool showChinese)
        {
            Model = model;
            _database = database;
            ShowChinese = showChinese;

            // 初始化子视图模型时不要触发写回
            _updating = true;
            try
            {
                foreach (var keyword in NumericKeywords)
                {
                    Numerics.Add(CreateNumeric(keyword));
                }

                foreach (var keyword in BooleanKeywords)
                {
                    Booleans.Add(CreateBoolean(keyword));
                }
            }
            finally
            {
                _updating = false;
            }

            RebuildChips();
            RebuildOtherConditions();
        }

        public override bool IsSelectableRule => true;

        public FilterRule Model { get; }

        public event EventHandler? Changed;

        /// <summary>编辑前触发，用于压入撤销栈。</summary>
        public Action? BeforeEdit { get; set; }

        public bool ShowChinese { get; private set; }

        public string Title { get; set; } = string.Empty;

        public bool IsShow => Model.IsShow;

        public bool IsDisabled => Model.IsDisabled;

        public string BadgeText => Model.IsShow ? "SHOW" : "HIDE";

        public string BadgeColor => Model.IsShow ? "#3E8E63" : "#B23A3A";

        public string StatusText => Model.IsDisabled ? "已停用" : Model.IsShow ? "显示" : "隐藏";

        /// <summary>绑定用：是否显示（Show）。</summary>
        public bool ShowEnabled
        {
            get => Model.IsShow;
            set => SetShow(value);
        }

        /// <summary>绑定用：是否停用（整块注释）。</summary>
        public bool DisabledEnabled
        {
            get => Model.IsDisabled;
            set => SetDisabled(value);
        }

        public ObservableCollection<ItemChipViewModel> ItemChips { get; } = new();

        public ObservableCollection<ItemChipViewModel> ClassChips { get; } = new();

        public ObservableCollection<NumericConditionViewModel> Numerics { get; } = new();

        public ObservableCollection<BooleanConditionViewModel> Booleans { get; } = new();

        public ObservableCollection<string> OtherConditions { get; } = new();

        public bool HasItemChips => ItemChips.Count > 0;

        public bool HasClassChips => ClassChips.Count > 0;

        public bool HasOtherConditions => OtherConditions.Count > 0;

        /// <summary>列表里的主标题：优先显示物品中文名。</summary>
        public string Headline
        {
            get
            {
                if (ItemChips.Count > 0)
                {
                    var names = string.Join("、", ItemChips.Take(3).Select(c => c.Display));
                    return ItemChips.Count > 3 ? $"{names} 等 {ItemChips.Count} 项" : names;
                }

                if (ClassChips.Count > 0)
                {
                    var names = string.Join("、", ClassChips.Take(2).Select(c => c.Display));
                    return ClassChips.Count > 2 ? $"{names} 等" : names;
                }

                return string.IsNullOrWhiteSpace(Title) ? (Model.IsShow ? "未命名规则" : "隐藏规则") : Title!;
            }
        }

        /// <summary>列表里的副标题：类别 + 稀有度 + 条件数。</summary>
        public string Subline
        {
            get
            {
                var parts = new List<string>();
                if (ItemChips.Count > 0 && ClassChips.Count > 0)
                {
                    parts.Add(ClassChips[0].Display);
                }

                var rarity = Model.FindCondition("Rarity")?.FirstValue;
                if (!string.IsNullOrEmpty(rarity))
                {
                    parts.Add(FilterCatalog.RarityToChinese(rarity!));
                }

                var extra = Model.Conditions.Count(c => !ManagedKeywords.Contains(c.Keyword) && !NumericKeywords.Contains(c.Keyword, StringComparer.OrdinalIgnoreCase));
                if (extra > 0)
                {
                    parts.Add($"{extra} 项额外条件");
                }

                if (Model.IsDisabled)
                {
                    parts.Add("已停用");
                }

                return parts.Count == 0 ? "无附加条件" : string.Join(" · ", parts);
            }
        }

        // ==================== 基础 ====================

        public void SetShow(bool isShow)
        {
            if (Model.IsShow == isShow)
            {
                return;
            }

            BeforeEdit?.Invoke();
            Model.IsShow = isShow;
            Model.MarkModified();
            RaisePropertyChanged(nameof(IsShow), nameof(ShowEnabled), nameof(BadgeText), nameof(BadgeColor), nameof(StatusText), nameof(Headline));
            OnChanged();
        }

        public void SetDisabled(bool disabled)
        {
            if (Model.IsDisabled == disabled)
            {
                return;
            }

            BeforeEdit?.Invoke();
            Model.IsDisabled = disabled;
            Model.MarkModified();
            RaisePropertyChanged(nameof(IsDisabled), nameof(DisabledEnabled), nameof(StatusText));
            OnChanged();
        }

        // ==================== 稀有度 ====================

        public string RarityValue
        {
            get => Model.FindCondition("Rarity")?.FirstValue ?? string.Empty;
            set
            {
                if (RarityValue == (value ?? string.Empty))
                {
                    return;
                }

                BeforeEdit?.Invoke();
                Model.SetConditionValue("Rarity", value);
                RaisePropertyChanged(nameof(RarityValue), nameof(Subline), nameof(UniqueHitSummary), nameof(HasUniqueHits));
                OnChanged();
            }
        }

        public IReadOnlyList<NamedChoice> RarityChoices => NamedChoice.Rarities;

        // ==================== 物品 / 类别 ====================

        public void AddItems(IEnumerable<ItemEntry> entries)
        {
            var values = Model.FindCondition("BaseType")?.Values.ToList() ?? new List<string>();
            var before = values.Count;
            foreach (var entry in entries)
            {
                if (values.All(v => !v.Equals(entry.NameEn, StringComparison.OrdinalIgnoreCase)))
                {
                    values.Add(entry.NameEn);
                }
            }

            if (values.Count == before)
            {
                return;
            }

            BeforeEdit?.Invoke();
            Model.SetConditionValues("BaseType", values);
            RebuildChips();
            OnChanged();
        }

        public void RemoveItem(string nameEn)
        {
            var values = Model.FindCondition("BaseType")?.Values.Where(v => !v.Equals(nameEn, StringComparison.OrdinalIgnoreCase)).ToList();
            if (values == null)
            {
                return;
            }

            BeforeEdit?.Invoke();
            Model.SetConditionValues("BaseType", values);
            RebuildChips();
            OnChanged();
        }

        public void AddClasses(IEnumerable<string> classNames)
        {
            var values = Model.FindCondition("Class")?.Values.ToList() ?? new List<string>();
            var before = values.Count;
            foreach (var name in classNames)
            {
                if (values.All(v => !v.Equals(name, StringComparison.OrdinalIgnoreCase)))
                {
                    values.Add(name);
                }
            }

            if (values.Count == before)
            {
                return;
            }

            BeforeEdit?.Invoke();
            Model.SetConditionValues("Class", values);
            RebuildChips();
            OnChanged();
        }

        public void RemoveClass(string nameEn)
        {
            var values = Model.FindCondition("Class")?.Values.Where(v => !v.Equals(nameEn, StringComparison.OrdinalIgnoreCase)).ToList();
            if (values == null)
            {
                return;
            }

            BeforeEdit?.Invoke();
            Model.SetConditionValues("Class", values);
            RebuildChips();
            OnChanged();
        }

        /// <summary>
        /// 按传奇添加过滤：过滤器无法直接匹配传奇名，只能写「基底 + Rarity Unique」。
        /// </summary>
        public void AddUniques(IEnumerable<UniqueEntry> uniques)
        {
            var list = uniques.ToList();
            if (list.Count == 0)
            {
                return;
            }

            var bases = list.Select(u => u.BaseEn)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (bases.Count == 0)
            {
                return;
            }

            var values = Model.FindCondition("BaseType")?.Values.ToList() ?? new List<string>();
            var addedBase = false;
            foreach (var baseName in bases)
            {
                if (values.All(v => !v.Equals(baseName, StringComparison.OrdinalIgnoreCase)))
                {
                    values.Add(baseName);
                    addedBase = true;
                }
            }

            var rarityChanged = !string.Equals(RarityValue, "Unique", StringComparison.OrdinalIgnoreCase);
            if (!addedBase && !rarityChanged)
            {
                return;
            }

            BeforeEdit?.Invoke();
            Model.SetConditionValue("Rarity", "Unique");
            if (addedBase)
            {
                Model.SetConditionValues("BaseType", values);
            }

            RebuildChips();
            RaisePropertyChanged(nameof(RarityValue), nameof(Subline), nameof(Headline),
                nameof(UniqueHitSummary), nameof(HasUniqueHits));
            OnChanged();
        }

        /// <summary>当前「传奇 + 基底」会命中的传奇（仅用于界面提示）。</summary>
        public IReadOnlyList<UniqueEntry> CoveredUniques
        {
            get
            {
                if (!string.Equals(RarityValue, "Unique", StringComparison.OrdinalIgnoreCase))
                {
                    return Array.Empty<UniqueEntry>();
                }

                var bases = Model.FindCondition("BaseType")?.Values;
                return bases == null || bases.Count == 0
                    ? Array.Empty<UniqueEntry>()
                    : _database.UniquesForBases(bases);
            }
        }

        public bool HasUniqueHits => CoveredUniques.Count > 0;

        public string UniqueHitSummary
        {
            get
            {
                var list = CoveredUniques;
                if (list.Count == 0)
                {
                    return string.Empty;
                }

                var names = string.Join("、", list.Take(8).Select(u => u.NameCn));
                var tail = list.Count > 8 ? $" 等 {list.Count} 件" : $"（共 {list.Count} 件）";
                return $"传奇稀有度 + 当前基底会命中：{names}{tail}";
            }
        }

        private void RebuildChips()
        {
            ItemChips.Clear();
            foreach (var value in Model.FindCondition("BaseType")?.Values ?? Enumerable.Empty<string>())
            {
                ItemChips.Add(new ItemChipViewModel(_database.Lookup(value), ShowChinese));
            }

            ClassChips.Clear();
            foreach (var value in Model.FindCondition("Class")?.Values ?? Enumerable.Empty<string>())
            {
                var entry = new ItemEntry(value, _database.ChineseClassName(value), value, _database.ChineseClassName(value));
                ClassChips.Add(new ItemChipViewModel(entry, ShowChinese));
            }

            RaisePropertyChanged(nameof(HasItemChips), nameof(HasClassChips), nameof(Headline), nameof(Subline),
                nameof(UniqueHitSummary), nameof(HasUniqueHits));
        }

        private void RebuildOtherConditions()
        {
            OtherConditions.Clear();
            foreach (var condition in Model.Conditions)
            {
                if (ManagedKeywords.Contains(condition.Keyword)
                    || NumericKeywords.Contains(condition.Keyword, StringComparer.OrdinalIgnoreCase)
                    || BooleanKeywords.Contains(condition.Keyword, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                var op = condition.Op == CompareOp.Implicit ? string.Empty : condition.OpSymbol + " ";
                OtherConditions.Add($"{FilterCatalog.ChineseOfCondition(condition.Keyword)}：{op}{string.Join("、", condition.Values)}");
            }

            foreach (var extra in Model.ExtraLines)
            {
                OtherConditions.Add("原始行：" + extra.Trim());
            }

            RaisePropertyChanged(nameof(HasOtherConditions));
        }

        internal void SetShowChinese(bool show)
        {
            ShowChinese = show;
            foreach (var chip in ItemChips.Concat(ClassChips))
            {
                chip.ShowChinese = show;
                chip.RefreshDisplay();
            }

            RaisePropertyChanged(nameof(Headline), nameof(Subline));
        }

        // ==================== 数值条件 ====================

        private NumericConditionViewModel CreateNumeric(string keyword)
        {
            var condition = Model.FindCondition(keyword);
            var vm = new NumericConditionViewModel(this, keyword, FilterCatalog.ChineseOfCondition(keyword))
            {
                IsEnabled = condition != null,
                Operator = condition != null && condition.Op != CompareOp.Implicit
                    ? condition.OpSymbol
                    : ">=",
                ValueText = condition?.FirstValue ?? string.Empty
            };
            return vm;
        }

        internal void ApplyNumeric(NumericConditionViewModel vm)
        {
            if (_updating)
            {
                return;
            }

            _updating = true;
            try
            {
                if (!vm.IsEnabled)
                {
                    vm.HasError = false;
                    if (Model.FindCondition(vm.Keyword) != null)
                    {
                        BeforeEdit?.Invoke();
                        Model.RemoveCondition(vm.Keyword);
                        RebuildOtherConditions();
                        OnChanged();
                    }

                    return;
                }

                if (!int.TryParse(vm.ValueText, out var number))
                {
                    // 提示用户当前输入不合法，而不是静默忽略
                    vm.HasError = !string.IsNullOrWhiteSpace(vm.ValueText);
                    return;
                }

                vm.HasError = false;

                BeforeEdit?.Invoke();
                var condition = Model.GetOrCreateCondition(vm.Keyword);
                condition.Op = FilterCondition.ParseOp(vm.Operator);
                condition.Values.Clear();
                condition.Values.Add(number.ToString());
                Model.MarkModified();
                RaisePropertyChanged(nameof(Subline), nameof(Headline));
                OnChanged();
            }
            finally
            {
                _updating = false;
            }
        }

        // ==================== 布尔条件 ====================

        private BooleanConditionViewModel CreateBoolean(string keyword)
        {
            var condition = Model.FindCondition(keyword);
            return new BooleanConditionViewModel(this, keyword, FilterCatalog.ChineseOfCondition(keyword))
            {
                Value = condition?.FirstValue ?? string.Empty
            };
        }

        internal void ApplyBoolean(BooleanConditionViewModel vm)
        {
            if (_updating)
            {
                return;
            }

            _updating = true;
            try
            {
                if (string.IsNullOrEmpty(vm.Value))
                {
                    if (Model.FindCondition(vm.Keyword) != null)
                    {
                        BeforeEdit?.Invoke();
                        Model.RemoveCondition(vm.Keyword);
                        RebuildOtherConditions();
                        OnChanged();
                    }

                    return;
                }

                BeforeEdit?.Invoke();
                var condition = Model.GetOrCreateCondition(vm.Keyword);
                condition.Values.Clear();
                condition.Values.Add(vm.Value);
                Model.MarkModified();
                OnChanged();
            }
            finally
            {
                _updating = false;
            }
        }

        // ==================== 生成文本 ====================

        public string RuleText => string.Join(Environment.NewLine, FilterWriter.WriteRule(Model, false));

        public void RefreshText()
        {
            RaisePropertyChanged(nameof(RuleText));
        }

        private void OnChanged()
        {
            RefreshText();
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

}
