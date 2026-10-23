using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Poe2FilterEditor.Models;
using Poe2FilterEditor.Services;
using Poe2FilterEditor.ViewModels;

namespace Poe2FilterEditor.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private MediaPlayer? _mediaPlayer;

        public MainWindow()
        {
            InitializeComponent();
            Infrastructure.WindowChromeHelper.ApplyDarkTitleBar(this);

            _vm = new MainViewModel();
            DataContext = _vm;
            _vm.PropertyChanged += OnViewModelPropertyChanged;

            Loaded += OnLoaded;
            Closing += OnClosing;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (_vm.Settings.WindowWidth > MinWidth && _vm.Settings.WindowHeight > MinHeight)
            {
                Width = _vm.Settings.WindowWidth;
                Height = _vm.Settings.WindowHeight;
            }

            UpdatePlaceholder();
        }

        private void OnClosing(object? sender, CancelEventArgs e)
        {
            if (!_vm.ConfirmDiscardChanges())
            {
                e.Cancel = true;
                return;
            }

            _vm.Settings.WindowWidth = Width;
            _vm.Settings.WindowHeight = Height;
            SettingsService.Save(_vm.Settings);
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedBlock) && _vm.SelectedBlock != null)
            {
                PreviewList.ScrollIntoView(_vm.SelectedBlock);
            }
            else if (e.PropertyName == nameof(MainViewModel.SearchText))
            {
                UpdatePlaceholder();
            }
        }

        private void UpdatePlaceholder()
        {
            SearchPlaceholder.Visibility = string.IsNullOrEmpty(_vm.SearchText) ? Visibility.Visible : Visibility.Collapsed;
        }

        // ==================== 快捷键 ====================

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;
            var inTextBox = Keyboard.FocusedElement is TextBox;

            if (ctrl && e.Key == Key.S)
            {
                if (shift)
                {
                    _vm.SaveAs();
                }
                else
                {
                    _vm.Save();
                }

                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.O)
            {
                _vm.OpenCommand.Execute(null);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.N)
            {
                _vm.NewCommand.Execute(null);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.Z && !inTextBox)
            {
                _vm.UndoCommand.Execute(null);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.Y && !inTextBox)
            {
                _vm.RedoCommand.Execute(null);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.D && !inTextBox)
            {
                _vm.DuplicateRuleCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Delete && !inTextBox)
            {
                _vm.DeleteRuleCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.F5)
            {
                _vm.ReloadCommand.Execute(null);
                e.Handled = true;
            }
        }

        // ==================== 物品 / 类别 ====================

        private void OnAddItem(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedRule == null)
            {
                MessageBox.Show("请先在左侧选择一条规则。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var items = ItemPickerWindow.PickItems(this, _vm.Database);
            if (items != null && items.Count > 0)
            {
                _vm.AddItemsToSelected(items);
            }
        }

        private void OnAddClass(object sender, RoutedEventArgs e)
        {
            if (_vm.SelectedRule == null)
            {
                MessageBox.Show("请先在左侧选择一条规则。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var classes = ItemPickerWindow.PickClasses(this, _vm.Database);
            if (classes != null && classes.Count > 0)
            {
                _vm.AddClassesToSelected(classes);
            }
        }

        private void OnRemoveItemChip(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string name })
            {
                _vm.RemoveItemFromSelected(name);
            }
        }

        private void OnRemoveClassChip(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: string name })
            {
                _vm.RemoveClassFromSelected(name);
            }
        }

        // ==================== 颜色 ====================

        private void OnPickBorderColor(object sender, RoutedEventArgs e) => PickColorFor("SetBorderColor", r => r.BorderColor, (r, c) => r.BorderColor = c);

        private void OnPickBackgroundColor(object sender, RoutedEventArgs e) => PickColorFor("SetBackgroundColor", r => r.BackgroundColor, (r, c) => r.BackgroundColor = c);

        private void OnPickTextColor(object sender, RoutedEventArgs e) => PickColorFor("SetTextColor", r => r.TextColor, (r, c) => r.TextColor = c);

        private void PickColorFor(string keyword, Func<RuleViewModel, FilterColor> getter, Action<RuleViewModel, FilterColor> setter)
        {
            var rule = _vm.SelectedRule;
            if (rule == null)
            {
                return;
            }

            var picker = new ColorPickerWindow(FilterCatalog.ChineseOfAction(keyword), getter(rule))
            {
                Owner = this
            };

            if (picker.ShowDialog() == true)
            {
                setter(rule, picker.SelectedColor);
            }
        }

        private void OnClearBorderColor(object sender, RoutedEventArgs e) => _vm.SelectedRule?.RemoveAction("SetBorderColor");

        private void OnClearBackgroundColor(object sender, RoutedEventArgs e) => _vm.SelectedRule?.RemoveAction("SetBackgroundColor");

        private void OnClearTextColor(object sender, RoutedEventArgs e) => _vm.SelectedRule?.RemoveAction("SetTextColor");

        private void OnClearEffect(object sender, RoutedEventArgs e) => _vm.SelectedRule?.RemoveAction("PlayEffect");

        private void OnClearMinimap(object sender, RoutedEventArgs e) => _vm.SelectedRule?.RemoveAction("MinimapIcon");

        // ==================== 音效 ====================

        private void OnBrowseSound(object sender, RoutedEventArgs e)
        {
            var rule = _vm.SelectedRule;
            if (rule == null)
            {
                return;
            }

            var filterDirectory = _vm.Document.FilePath == null ? null : Path.GetDirectoryName(_vm.Document.FilePath);
            var picked = SoundService.PickFolder(filterDirectory);
            if (picked == null)
            {
                return;
            }

            rule.SoundPath = ToFilterRelativePath(picked, filterDirectory);
        }

        private string ToFilterRelativePath(string file, string? filterDirectory)
        {
            if (string.IsNullOrEmpty(filterDirectory))
            {
                return Path.GetFileName(file);
            }

            try
            {
                var relative = Path.GetRelativePath(filterDirectory!, file);
                if (!relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative))
                {
                    return relative.Replace('\\', '/');
                }
            }
            catch
            {
                // 忽略，回退为绝对路径
            }

            return file.Replace('\\', '/');
        }

        private void OnGenerateSoundPath(object sender, RoutedEventArgs e)
        {
            var rule = _vm.SelectedRule;
            if (rule == null)
            {
                return;
            }

            var suggestion = rule.SuggestSoundPath();
            if (!string.IsNullOrEmpty(suggestion))
            {
                rule.SoundPath = suggestion;
            }
        }

        private void OnPlaySound(object sender, RoutedEventArgs e)
        {
            var rule = _vm.SelectedRule;
            if (rule == null || string.IsNullOrWhiteSpace(rule.SoundPath))
            {
                return;
            }

            var resolved = SoundService.Resolve(rule.SoundPath,
                _vm.Document.FilePath, _vm.Settings.SoundRootDirectory);

            if (resolved == null || !File.Exists(resolved))
            {
                MessageBox.Show("本地没有找到这个音效文件，无法试听。", "试听", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                _mediaPlayer?.Close();
                _mediaPlayer = new MediaPlayer();
                _mediaPlayer.Open(new Uri(Path.GetFullPath(resolved)));
                _mediaPlayer.Play();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"播放失败：{ex.Message}", "试听", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnClearSound(object sender, RoutedEventArgs e)
        {
            _vm.SelectedRule?.RemoveAction("CustomAlertSound");
            _vm.SelectedRule?.RemoveAction("CustomAlertSoundOptional");
        }

        // ==================== 其他 ====================

        private void OnToggleShow(object sender, RoutedEventArgs e)
        {
            var rule = _vm.SelectedRule;
            if (rule != null)
            {
                rule.SetShow(!rule.IsShow);
            }
        }

        private void OnToggleDisable(object sender, RoutedEventArgs e)
        {
            var rule = _vm.SelectedRule;
            if (rule != null)
            {
                rule.SetDisabled(!rule.IsDisabled);
            }
        }

        private void OnOpenSettings(object sender, RoutedEventArgs e)
        {
            var window = new SettingsWindow(_vm) { Owner = this };
            window.ShowDialog();
        }

        private void OnShowIssues(object sender, RoutedEventArgs e)
        {
            _vm.ShowIssues = !_vm.ShowIssues;
        }

        private void OnCloseIssues(object sender, RoutedEventArgs e)
        {
            _vm.ShowIssues = false;
        }

        private void OnIssueDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox { SelectedItem: ValidationIssue issue })
            {
                _vm.SelectIssue(issue);
            }
        }
    }
}
