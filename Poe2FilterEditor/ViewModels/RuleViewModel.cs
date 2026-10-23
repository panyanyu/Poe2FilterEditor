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
    public sealed class RuleViewModel : PaneEntry
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
                RaisePropertyChanged(nameof(RarityValue), nameof(Subline));
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

            RaisePropertyChanged(nameof(HasItemChips), nameof(HasClassChips), nameof(Headline), nameof(Subline));
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
                    return;
                }

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

        // ==================== 外观 ====================

        public bool HasBorderColor => Model.HasAction("SetBorderColor");

        public bool HasBackgroundColor => Model.HasAction("SetBackgroundColor");

        public bool HasTextColor => Model.HasAction("SetTextColor");

        public bool HasFontSize => Model.HasAction("SetFontSize");

        public bool HasEffect => Model.HasAction("PlayEffect");

        public bool HasMinimap => Model.HasAction("MinimapIcon");

        public bool HasSound => Model.HasAction("CustomAlertSound") || Model.HasAction("CustomAlertSoundOptional");

        public FilterColor BorderColor
        {
            get => ReadColor("SetBorderColor");
            set => WriteColor("SetBorderColor", value);
        }

        public FilterColor BackgroundColor
        {
            get => ReadColor("SetBackgroundColor");
            set => WriteColor("SetBackgroundColor", value);
        }

        public FilterColor TextColor
        {
            get => ReadColor("SetTextColor");
            set => WriteColor("SetTextColor", value);
        }

        public Brush BorderColorBrush => BorderColor.ToBrush();

        public Brush BackgroundColorBrush => BackgroundColor.ToBrush();

        public Brush TextColorBrush => TextColor.ToBrush();

        public Brush PreviewBorderBrush => HasBorderColor ? BorderColor.ToBrush() : new SolidColorBrush(Color.FromRgb(0x3A, 0x33, 0x2B));

        public Brush PreviewBackgroundBrush => HasBackgroundColor
            ? BackgroundColor.ToBrush()
            : new SolidColorBrush(Color.FromArgb(0xCC, 0x14, 0x11, 0x10));

        public Brush PreviewTextBrush => HasTextColor ? TextColor.ToBrush() : new SolidColorBrush(Color.FromRgb(0xE8, 0xE2, 0xD6));

        public double PreviewFontSize => Math.Max(11, Math.Min(28, FontSizeInt ?? 14));

        private int? FontSizeInt => int.TryParse(FontSizeText, out var size) ? size : null;

        public string FontSizeText
        {
            get => Model.GetAction("SetFontSize")?.ArgAt(0) ?? string.Empty;
            set
            {
                if (FontSizeText == (value ?? string.Empty))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    RemoveAction("SetFontSize");
                    return;
                }

                if (!int.TryParse(value, out var size) || size < 1 || size > 45)
                {
                    RaisePropertyChanged(nameof(FontSizeText));
                    return;
                }

                BeforeEdit?.Invoke();
                var action = Model.GetOrCreateAction("SetFontSize");
                action.Args.Clear();
                action.Args.Add(size.ToString());
                Model.MarkModified();
                RaisePropertyChanged(nameof(FontSizeText), nameof(HasFontSize), nameof(PreviewFontSize));
                OnChanged();
            }
        }

        public string EffectColor
        {
            get => Model.GetAction("PlayEffect")?.ArgAt(0) ?? string.Empty;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    RemoveAction("PlayEffect");
                    return;
                }

                BeforeEdit?.Invoke();
                var action = Model.GetOrCreateAction("PlayEffect");
                action.Args.Clear();
                action.Args.Add(value);
                if (EffectTemp)
                {
                    action.Args.Add("Temp");
                }

                Model.MarkModified();
                RaisePropertyChanged(nameof(EffectColor), nameof(HasEffect));
                OnChanged();
            }
        }

        public bool EffectTemp
        {
            get => Model.GetAction("PlayEffect")?.Args.Any(a => a.Equals("Temp", StringComparison.OrdinalIgnoreCase)) == true;
            set
            {
                var action = Model.GetAction("PlayEffect");
                if (action == null)
                {
                    return;
                }

                BeforeEdit?.Invoke();
                action.Args.RemoveAll(a => a.Equals("Temp", StringComparison.OrdinalIgnoreCase));
                if (value)
                {
                    action.Args.Add("Temp");
                }

                Model.MarkModified();
                RaisePropertyChanged(nameof(EffectTemp));
                OnChanged();
            }
        }

        public string MinimapSize
        {
            get => Model.GetAction("MinimapIcon")?.ArgAt(0) ?? string.Empty;
            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    RemoveAction("MinimapIcon");
                    return;
                }

                WriteMinimap(value, MinimapColorValue, MinimapShapeValue);
            }
        }

        public string MinimapColorValue
        {
            get => Model.GetAction("MinimapIcon")?.ArgAt(1) ?? "Red";
            set => WriteMinimap(MinimapSize, value, MinimapShapeValue);
        }

        public string MinimapShapeValue
        {
            get => Model.GetAction("MinimapIcon")?.ArgAt(2) ?? "Circle";
            set => WriteMinimap(MinimapSize, MinimapColorValue, value);
        }

        private void WriteMinimap(string size, string color, string shape)
        {
            BeforeEdit?.Invoke();
            var action = Model.GetOrCreateAction("MinimapIcon");
            action.Args.Clear();
            action.Args.Add(string.IsNullOrEmpty(size) ? "0" : size);
            action.Args.Add(string.IsNullOrEmpty(color) ? "Red" : color);
            action.Args.Add(string.IsNullOrEmpty(shape) ? "Circle" : shape);
            Model.MarkModified();
            RaisePropertyChanged(nameof(MinimapSize), nameof(MinimapColorValue), nameof(MinimapShapeValue), nameof(HasMinimap));
            OnChanged();
        }

        public string SoundPath
        {
            get => Model.GetAction("CustomAlertSound")?.ArgAt(0)
                   ?? Model.GetAction("CustomAlertSoundOptional")?.ArgAt(0)
                   ?? string.Empty;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    RemoveAction("CustomAlertSound");
                    return;
                }

                BeforeEdit?.Invoke();
                var action = Model.GetOrCreateAction("CustomAlertSound");
                action.Args.Clear();
                action.Args.Add(value.Trim().Replace('\\', '/'));
                if (int.TryParse(VolumeText, out var volume))
                {
                    action.Args.Add(volume.ToString());
                }

                Model.MarkModified();
                RaisePropertyChanged(nameof(SoundPath), nameof(HasSound), nameof(SoundStatus));
                OnChanged();
            }
        }

        /// <summary>音效状态提示（是否能在本地找到文件）。</summary>
        public string SoundStatus
        {
            get
            {
                if (string.IsNullOrWhiteSpace(SoundPath))
                {
                    return "尚未设置音效。可以点「按物品中文名生成路径」快速生成。";
                }

                if (SoundService.Exists(SoundPath, null, null))
                {
                    return "已找到本地音效文件。";
                }

                return "本地未找到该文件，只要客户端目录结构一致，游戏内仍可正常播放。";
            }
        }

        /// <summary>按物品中文名生成 sounds/中文名.mp3 路径。</summary>
        public string? SuggestSoundPath()
        {
            var name = ItemChips.FirstOrDefault()?.Entry.NameCn
                       ?? ClassChips.FirstOrDefault()?.Entry.NameCn
                       ?? "掉落提示";
            return SoundService.SuggestPath(name);
        }

        public IReadOnlyList<NamedChoice> EffectColorChoices => NamedChoice.EffectColors;

        public IReadOnlyList<NamedChoice> IconSizeChoices => NamedChoice.IconSizes;

        public IReadOnlyList<NamedChoice> IconColorChoices => NamedChoice.IconColors;

        public IReadOnlyList<NamedChoice> IconShapeChoices => NamedChoice.IconShapes;

        public string VolumeText
        {
            get => Model.GetAction("CustomAlertSound")?.ArgAt(1) ?? string.Empty;
            set
            {
                var action = Model.GetAction("CustomAlertSound");
                if (action == null)
                {
                    RaisePropertyChanged(nameof(VolumeText));
                    return;
                }

                if (!int.TryParse(value, out var volume) || volume < 0 || volume > 400)
                {
                    RaisePropertyChanged(nameof(VolumeText));
                    return;
                }

                BeforeEdit?.Invoke();
                if (action.Args.Count > 1)
                {
                    action.Args[1] = volume.ToString();
                }
                else
                {
                    action.Args.Add(volume.ToString());
                }

                Model.MarkModified();
                RaisePropertyChanged(nameof(VolumeText));
                OnChanged();
            }
        }

        public void RemoveAction(string keyword)
        {
            if (!Model.HasAction(keyword))
            {
                return;
            }

            BeforeEdit?.Invoke();
            Model.RemoveAction(keyword);
            RaisePropertyChanged(nameof(HasBorderColor), nameof(HasBackgroundColor), nameof(HasTextColor),
                nameof(HasFontSize), nameof(HasEffect), nameof(HasMinimap), nameof(HasSound),
                nameof(BorderColor), nameof(BorderColorBrush), nameof(BackgroundColor), nameof(BackgroundColorBrush),
                nameof(TextColor), nameof(TextColorBrush), nameof(FontSizeText), nameof(EffectColor), nameof(MinimapSize),
                nameof(SoundPath), nameof(VolumeText), nameof(SoundStatus), nameof(PreviewBorderBrush),
                nameof(PreviewBackgroundBrush), nameof(PreviewTextBrush), nameof(PreviewFontSize));
            OnChanged();
        }

        private FilterColor ReadColor(string keyword)
        {
            var action = Model.GetAction(keyword);
            if (action == null)
            {
                return keyword == "SetTextColor" ? FilterColor.White : new FilterColor(200, 200, 200, 255);
            }

            return FilterColor.Parse(action.ArgAt(0), action.ArgAt(1), action.ArgAt(2), action.ArgAt(3));
        }

        private void WriteColor(string keyword, FilterColor color)
        {
            BeforeEdit?.Invoke();
            var action = Model.GetOrCreateAction(keyword);
            action.Args.Clear();
            action.Args.Add(color.R.ToString());
            action.Args.Add(color.G.ToString());
            action.Args.Add(color.B.ToString());
            action.Args.Add(color.A.ToString());
            Model.MarkModified();
            RaisePropertyChanged(nameof(BorderColor), nameof(BorderColorBrush), nameof(BackgroundColor),
                nameof(BackgroundColorBrush), nameof(TextColor), nameof(TextColorBrush), nameof(HasBorderColor),
                nameof(HasBackgroundColor), nameof(HasTextColor), nameof(PreviewBorderBrush),
                nameof(PreviewBackgroundBrush), nameof(PreviewTextBrush));
            OnChanged();
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

    /// <summary>数值条件（物品等级、掉落等级…）。</summary>
    public sealed class NumericConditionViewModel : ObservableObject
    {
        private readonly RuleViewModel _owner;
        private bool _isEnabled;
        private string _operator = ">=";
        private string _valueText = string.Empty;

        public NumericConditionViewModel(RuleViewModel owner, string keyword, string chineseName)
        {
            _owner = owner;
            Keyword = keyword;
            ChineseName = chineseName;
        }

        public string Keyword { get; }

        public string ChineseName { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (SetProperty(ref _isEnabled, value))
                {
                    _owner.ApplyNumeric(this);
                }
            }
        }

        public string Operator
        {
            get => _operator;
            set
            {
                if (SetProperty(ref _operator, value))
                {
                    _owner.ApplyNumeric(this);
                }
            }
        }

        public string ValueText
        {
            get => _valueText;
            set
            {
                if (SetProperty(ref _valueText, value))
                {
                    _owner.ApplyNumeric(this);
                }
            }
        }

        public IReadOnlyList<NamedChoice> OperatorChoices => NamedChoice.Operators;
    }

    /// <summary>布尔条件（已腐化、已鉴定…），三态：不限 / 是 / 否。</summary>
    public sealed class BooleanConditionViewModel : ObservableObject
    {
        private readonly RuleViewModel _owner;
        private string _value = string.Empty;

        public BooleanConditionViewModel(RuleViewModel owner, string keyword, string chineseName)
        {
            _owner = owner;
            Keyword = keyword;
            ChineseName = chineseName;
        }

        public string Keyword { get; }

        public string ChineseName { get; }

        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value ?? string.Empty))
                {
                    _owner.ApplyBoolean(this);
                }
            }
        }

        public IReadOnlyList<NamedChoice> Choices => NamedChoice.Booleans;
    }
}
