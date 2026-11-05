using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using Poe2FilterEditor.Infrastructure;
using Poe2FilterEditor.Models;
using Poe2FilterEditor.Services;

namespace Poe2FilterEditor.ViewModels
{
    /// <summary>规则的外观与音效（颜色、字号、光柱、小地图图标、提示音）。</summary>
    public sealed partial class RuleViewModel
    {
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
            RaisePropertyChanged(nameof(MinimapSize), nameof(MinimapColorValue), nameof(MinimapShapeValue),
                nameof(HasMinimap), nameof(CanEditMinimap));
            OnChanged();
        }

        /// <summary>没有选图标大小时，颜色/形状下拉不可编辑，避免误建 MinimapIcon。</summary>
        public bool CanEditMinimap => !string.IsNullOrEmpty(MinimapSize);

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

                // 先读出音量，再清空参数（否则清空后就读不到了）
                var volumeText = VolumeText;

                // 已经有 CustomAlertSoundOptional 时沿用，避免同一条规则里并存两条音效动作
                var action = Model.GetAction("CustomAlertSound")
                             ?? Model.GetAction("CustomAlertSoundOptional")
                             ?? Model.GetOrCreateAction("CustomAlertSound");
                action.Args.Clear();
                action.Args.Add(value.Trim().Replace('\\', '/'));
                if (int.TryParse(volumeText, out var volume))
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
            get => Model.GetAction("CustomAlertSound")?.ArgAt(1)
                   ?? Model.GetAction("CustomAlertSoundOptional")?.ArgAt(1)
                   ?? string.Empty;
            set
            {
                var action = Model.GetAction("CustomAlertSound") ?? Model.GetAction("CustomAlertSoundOptional");
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
                nameof(HasFontSize), nameof(HasEffect), nameof(HasMinimap), nameof(HasSound), nameof(CanEditMinimap),
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

    }
}
