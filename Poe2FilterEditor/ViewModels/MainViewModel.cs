using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Poe2FilterEditor.Infrastructure;
using Poe2FilterEditor.Models;
using Poe2FilterEditor.Services;

namespace Poe2FilterEditor.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private readonly List<string> _undo = new();
        private readonly List<string> _redo = new();
        private readonly DispatcherTimer _refreshTimer;

        private FilterDocument _document = new();
        private PaneEntry? _selectedEntry;
        private PreviewBlockViewModel? _selectedBlock;
        private string _searchText = string.Empty;
        private string _statusMessage = "就绪";
        private bool _showIssues;

        public MainViewModel()
        {
            Settings = SettingsService.Load();
            Database = ItemDatabase.Load(AppContext.BaseDirectory,
                Settings.UserMappingFile ?? SettingsService.DefaultUserMappingFile);

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(180) };
            _refreshTimer.Tick += (_, _) =>
            {
                _refreshTimer.Stop();
                RebuildPreview();
                Validate();
            };

            NewCommand = new RelayCommand(() => NewDocument(true));
            OpenCommand = new RelayCommand(OpenFile);
            SaveCommand = new RelayCommand(() => Save());
            SaveAsCommand = new RelayCommand(() => SaveAs());
            AddRuleCommand = new RelayCommand(AddRule);
            DuplicateRuleCommand = new RelayCommand(DuplicateRule, () => SelectedRule != null);
            DeleteRuleCommand = new RelayCommand(DeleteRule, () => SelectedRule != null);
            MoveUpCommand = new RelayCommand(() => MoveRule(-1), () => SelectedRule != null);
            MoveDownCommand = new RelayCommand(() => MoveRule(1), () => SelectedRule != null);
            ToggleDisableCommand = new RelayCommand(() => SelectedRule?.SetDisabled(!SelectedRule.IsDisabled), () => SelectedRule != null);
            ToggleShowCommand = new RelayCommand(() => SelectedRule?.SetShow(!SelectedRule.IsShow), () => SelectedRule != null);
            UndoCommand = new RelayCommand(Undo, () => CanUndo);
            RedoCommand = new RelayCommand(Redo, () => CanRedo);
            CopyPreviewCommand = new RelayCommand(CopyPreview);
            ReloadCommand = new RelayCommand(Reload, () => !string.IsNullOrEmpty(Document.FilePath));
            ShowIssuesCommand = new RelayCommand(() => ShowIssues = !ShowIssues);

            LoadStartupDocument();
        }

        public ItemDatabase Database { get; }

        public AppSettings Settings { get; }

        public FilterDocument Document => _document;

        public ObservableCollection<PaneEntry> Entries { get; } = new();

        public ObservableCollection<PreviewBlockViewModel> PreviewBlocks { get; } = new();

        public ObservableCollection<ValidationIssue> Issues { get; } = new();

        public ICommand NewCommand { get; }

        public ICommand OpenCommand { get; }

        public ICommand SaveCommand { get; }

        public ICommand SaveAsCommand { get; }

        public ICommand AddRuleCommand { get; }

        public ICommand DuplicateRuleCommand { get; }

        public ICommand DeleteRuleCommand { get; }

        public ICommand MoveUpCommand { get; }

        public ICommand MoveDownCommand { get; }

        public ICommand ToggleDisableCommand { get; }

        public ICommand ToggleShowCommand { get; }

        public ICommand UndoCommand { get; }

        public ICommand RedoCommand { get; }

        public ICommand CopyPreviewCommand { get; }

        public ICommand ReloadCommand { get; }

        public ICommand ShowIssuesCommand { get; }

        // ==================== 状态 ====================

        public string WindowTitle
        {
            get
            {
                var dot = _document.IsDirty ? " •" : string.Empty;
                return $"{_document.FileName}{dot} — POE2 国服过滤器编辑器";
            }
        }

        public string FilePathText => _document.DisplayPath;

        public string FileName => _document.FileName;

        public int RuleCount => _document.RuleCount;

        public string StatusText =>
            $"{_document.RuleCount} 条规则 · {Issues.Count} 个问题 · {EncodingText} · {NewLineText}";

        public string EncodingText => _document.HasBom ? "UTF-8 BOM" : "UTF-8";

        public string NewLineText => _document.NewLine == "\r\n" ? "CRLF" : "LF";

        public string IssueCountText => Issues.Count == 0 ? "无问题" : $"{Issues.Count} 个问题";

        public string IssueCountColor => Issues.Count == 0 ? "#6B6156" : "#E29A4A";

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool ShowIssues
        {
            get => _showIssues;
            set => SetProperty(ref _showIssues, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value ?? string.Empty))
                {
                    RebuildEntries();
                    RebuildPreview();
                    StatusMessage = string.IsNullOrWhiteSpace(_searchText) ? "就绪" : $"正在筛选：{_searchText}";
                }
            }
        }

        public bool ShowChineseNames
        {
            get => Settings.ShowChineseNames;
            set
            {
                if (Settings.ShowChineseNames == value)
                {
                    return;
                }

                Settings.ShowChineseNames = value;
                SettingsService.Save(Settings);
                foreach (var vm in Entries.OfType<RuleViewModel>())
                {
                    vm.SetShowChinese(value);
                }

                RaisePropertyChanged(nameof(ShowChineseNames));
            }
        }

        public bool NormalizeOperators
        {
            get => Settings.NormalizeOperatorsOnSave;
            set
            {
                if (Settings.NormalizeOperatorsOnSave == value)
                {
                    return;
                }

                Settings.NormalizeOperatorsOnSave = value;
                SettingsService.Save(Settings);
                RaisePropertyChanged(nameof(NormalizeOperators));
                ScheduleRefresh();
            }
        }

        public string SoundRootDirectory
        {
            get => Settings.SoundRootDirectory ?? string.Empty;
            set
            {
                var normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
                if (Settings.SoundRootDirectory == normalized)
                {
                    return;
                }

                Settings.SoundRootDirectory = normalized;
                SettingsService.Save(Settings);
                RaisePropertyChanged(nameof(SoundRootDirectory));
                Validate();
            }
        }

        public string MappingSummary =>
            $"内置映射 {Database.ItemCount} 条 · 类别 {Database.Classes.Count} 个 · 数据版本 {Database.Version ?? "未知"}\n{Database.Source}";

        public string MappingStatus { get; private set; } = string.Empty;

        /// <summary>导入外部映射 JSON，合并后写入用户覆盖文件。</summary>
        public void ImportMapping(string path)
        {
            try
            {
                var count = Database.MergeFromFile(path, true);
                Database.SaveUserEntries();
                RebuildEntries();
                RebuildPreview();
                Validate();
                MappingStatus = $"已导入 {count} 条映射，保存在 {Database.UserFilePath}";
            }
            catch (Exception ex)
            {
                MappingStatus = $"导入失败：{ex.Message}";
            }

            RaisePropertyChanged(nameof(MappingSummary), nameof(MappingStatus));
        }

        public PaneEntry? SelectedEntry
        {
            get => _selectedEntry;
            set
            {
                if (!SetProperty(ref _selectedEntry, value))
                {
                    return;
                }

                RaisePropertyChanged(nameof(SelectedRule), nameof(HasSelection), nameof(SelectionTitle));
                RaiseCommandStates();

                if (value is RuleViewModel rule)
                {
                    var block = PreviewBlocks.FirstOrDefault(b => ReferenceEquals(b.Rule, rule));
                    if (block != null)
                    {
                        SetSelectedBlock(block);
                    }
                }
            }
        }

        public RuleViewModel? SelectedRule => _selectedEntry as RuleViewModel;

        public bool HasSelection => SelectedRule != null;

        public string SelectionTitle => SelectedRule == null ? "未选择规则" : SelectedRule.Headline;

        public PreviewBlockViewModel? SelectedBlock
        {
            get => _selectedBlock;
            set
            {
                if (!SetProperty(ref _selectedBlock, value))
                {
                    return;
                }

                if (value?.Rule != null && !ReferenceEquals(value.Rule, SelectedRule))
                {
                    SelectedEntry = value.Rule;
                }
            }
        }

        public string PreviewText { get; private set; } = string.Empty;

        public bool CanUndo => _undo.Count > 0;

        public bool CanRedo => _redo.Count > 0;

        // ==================== 文件 ====================

        private void LoadStartupDocument()
        {
            var last = Settings.LastFilePath;
            if (!string.IsNullOrWhiteSpace(last) && File.Exists(last))
            {
                OpenPath(last!, false);
                return;
            }

            var sample = Path.Combine(AppContext.BaseDirectory, "xx-0.4.filter");
            if (File.Exists(sample))
            {
                OpenPath(sample, false);
                StatusMessage = "已载入示例过滤器 xx-0.4.filter，可另存为自己的过滤器。";
                return;
            }

            NewDocument(false);
        }

        private void OpenPath(string path, bool remember)
        {
            try
            {
                _document = FilterFileService.Load(path);
                if (remember)
                {
                    Settings.LastFilePath = path;
                    Settings.LastDirectory = Path.GetDirectoryName(path);
                    SettingsService.Save(Settings);
                }

                StatusMessage = $"已打开 {Path.GetFileName(path)}";
                AfterDocumentReplaced();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开文件失败：{ex.Message}", "打开失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFile()
        {
            if (!ConfirmDiscardChanges())
            {
                return;
            }

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "打开过滤器",
                Filter = "过滤器文件 (*.filter)|*.filter|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (!string.IsNullOrWhiteSpace(Settings.LastDirectory) && Directory.Exists(Settings.LastDirectory))
            {
                dialog.InitialDirectory = Settings.LastDirectory;
            }

            if (dialog.ShowDialog() == true)
            {
                OpenPath(dialog.FileName, true);
            }
        }

        private void NewDocument(bool confirm)
        {
            if (confirm && !ConfirmDiscardChanges())
            {
                return;
            }

            var stamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            var text = $"# POE2 国服物品过滤器\r\n# 创建于 {stamp}\r\n# 提示：规则中的物品名称在界面显示为国服中文名，文件内保存英文内部名。\r\n\r\n";
            _document = FilterParser.Parse(text, null);
            _document.EndsWithNewLine = true;
            _undo.Clear();
            _redo.Clear();
            AfterDocumentReplaced();
            StatusMessage = "已新建过滤器";
        }

        public bool Save()
        {
            if (string.IsNullOrEmpty(_document.FilePath))
            {
                return SaveAs();
            }

            return SaveTo(_document.FilePath!);
        }

        public bool SaveAs()
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "另存为过滤器",
                Filter = "过滤器文件 (*.filter)|*.filter|所有文件 (*.*)|*.*",
                FileName = _document.FilePath != null ? Path.GetFileName(_document.FilePath) : "my.filter",
                AddExtension = true,
                DefaultExt = ".filter"
            };

            if (!string.IsNullOrWhiteSpace(Settings.LastDirectory) && Directory.Exists(Settings.LastDirectory))
            {
                dialog.InitialDirectory = Settings.LastDirectory;
            }

            if (dialog.ShowDialog() != true)
            {
                return false;
            }

            return SaveTo(dialog.FileName);
        }

        private bool SaveTo(string path)
        {
            try
            {
                FilterFileService.Save(_document, path, Settings.NormalizeOperatorsOnSave);

                Settings.LastFilePath = path;
                Settings.LastDirectory = Path.GetDirectoryName(path);
                SettingsService.Save(Settings);

                foreach (var vm in Entries.OfType<RuleViewModel>())
                {
                    vm.RefreshText();
                }

                RebuildPreview();
                Validate();
                RaisePropertyChanged(nameof(IsDirty), nameof(WindowTitle), nameof(FilePathText), nameof(FileName),
                    nameof(StatusText));
                StatusMessage = $"已保存到 {path}";
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存失败：{ex.Message}", "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private void Reload()
        {
            if (string.IsNullOrEmpty(_document.FilePath))
            {
                return;
            }

            if (!ConfirmDiscardChanges())
            {
                return;
            }

            OpenPath(_document.FilePath!, false);
            StatusMessage = "已重新载入";
        }

        public bool IsDirty => _document.IsDirty;

        /// <summary>关闭前确认；返回 false 表示取消操作。</summary>
        public bool ConfirmDiscardChanges()
        {
            if (!_document.IsDirty)
            {
                return true;
            }

            var result = MessageBox.Show($"「{_document.FileName}」有未保存的修改，是否保存？", "POE2 过滤器编辑器",
                MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

            return result switch
            {
                MessageBoxResult.Yes => Save(),
                MessageBoxResult.No => true,
                _ => false
            };
        }

        // ==================== 列表 / 预览 ====================

        private void AfterDocumentReplaced()
        {
            _undo.Clear();
            _redo.Clear();
            RebuildEntries();
            RebuildPreview();
            Validate();
            RaisePropertyChanged(nameof(WindowTitle), nameof(FilePathText), nameof(FileName), nameof(RuleCount),
                nameof(IsDirty), nameof(StatusText), nameof(EncodingText), nameof(NewLineText),
                nameof(CanUndo), nameof(CanRedo));
            RaiseCommandStates();
        }

        private void RebuildEntries()
        {
            var previousModel = SelectedRule?.Model;
            var query = SearchText.Trim();
            var searching = query.Length > 0;

            Entries.Clear();

            foreach (var node in _document.Nodes)
            {
                switch (node)
                {
                    case CommentNode comment when !searching && comment.HasContent:
                        Entries.Add(new SectionEntry(comment.Text, comment.LineNumber));
                        break;
                    case FilterRule rule:
                    {
                        var vm = new RuleViewModel(rule, Database, Settings.ShowChineseNames)
                        {
                            Title = _document.TitleOf(rule) ?? string.Empty,
                            BeforeEdit = PushUndo
                        };
                        vm.Changed += OnRuleChanged;

                        if (searching
                            && !vm.Headline.Contains(query, StringComparison.OrdinalIgnoreCase)
                            && !vm.Subline.Contains(query, StringComparison.OrdinalIgnoreCase)
                            && !rule.Conditions.SelectMany(c => c.Values).Any(v => v.Contains(query, StringComparison.OrdinalIgnoreCase)))
                        {
                            continue;
                        }

                        Entries.Add(vm);
                        break;
                    }
                }
            }

            // 恢复选中
            var restored = previousModel == null
                ? null
                : Entries.OfType<RuleViewModel>().FirstOrDefault(v => ReferenceEquals(v.Model, previousModel));

            if (restored == null)
            {
                restored = Entries.OfType<RuleViewModel>().FirstOrDefault();
            }

            _selectedEntry = restored;
            RaisePropertyChanged(nameof(SelectedEntry), nameof(SelectedRule), nameof(HasSelection),
                nameof(SelectionTitle), nameof(RuleCount));
            RaiseCommandStates();
        }

        public void RebuildPreview()
        {
            var blocks = new List<PreviewBlockViewModel>();

            foreach (var node in _document.Nodes)
            {
                switch (node)
                {
                    case CommentNode comment:
                        blocks.Add(new PreviewBlockViewModel(string.Join("\n", comment.Lines), null, true));
                        break;
                    case BlankNode blank:
                        blocks.Add(new PreviewBlockViewModel(new string('\n', Math.Max(blank.Count, 1)), null, false));
                        break;
                    case RawNode raw:
                        blocks.Add(new PreviewBlockViewModel(raw.Text, null, false));
                        break;
                    case FilterRule rule:
                    {
                        var vm = Entries.OfType<RuleViewModel>().FirstOrDefault(v => ReferenceEquals(v.Model, rule));
                        var lines = FilterWriter.WriteRule(rule, Settings.NormalizeOperatorsOnSave);
                        blocks.Add(new PreviewBlockViewModel(string.Join("\n", lines), vm, false));
                        break;
                    }
                }
            }

            PreviewBlocks.Clear();
            foreach (var block in blocks)
            {
                PreviewBlocks.Add(block);
            }

            PreviewText = FilterWriter.Write(_document, Settings.NormalizeOperatorsOnSave);
            RaisePropertyChanged(nameof(PreviewText));

            var target = SelectedRule == null ? null : PreviewBlocks.FirstOrDefault(b => ReferenceEquals(b.Rule, SelectedRule));
            SetSelectedBlock(target);
        }

        private void SetSelectedBlock(PreviewBlockViewModel? block)
        {
            if (ReferenceEquals(_selectedBlock, block))
            {
                return;
            }

            if (_selectedBlock != null)
            {
                _selectedBlock.IsSelected = false;
            }

            _selectedBlock = block;
            if (_selectedBlock != null)
            {
                _selectedBlock.IsSelected = true;
            }

            RaisePropertyChanged(nameof(SelectedBlock));
        }

        private void OnRuleChanged(object? sender, EventArgs e)
        {
            _document.IsDirty = true;
            RaisePropertyChanged(nameof(IsDirty), nameof(WindowTitle));
            RaisePropertyChanged(nameof(SelectionTitle));
            ScheduleRefresh();
        }

        private void ScheduleRefresh()
        {
            _refreshTimer.Stop();
            _refreshTimer.Start();
        }

        private void Validate()
        {
            Issues.Clear();
            var issues = ValidationService.Validate(_document, Database, Settings.SoundRootDirectory);
            foreach (var issue in issues)
            {
                Issues.Add(issue);
            }

            RaisePropertyChanged(nameof(StatusText), nameof(IssueCountText), nameof(IssueCountColor));
        }

        public void SelectIssue(ValidationIssue? issue)
        {
            if (issue?.Rule == null)
            {
                return;
            }

            var vm = Entries.OfType<RuleViewModel>().FirstOrDefault(v => ReferenceEquals(v.Model, issue.Rule));
            if (vm != null)
            {
                SelectedEntry = vm;
            }
        }

        // ==================== 规则编辑 ====================

        public void AddRule()
        {
            PushUndo();

            var rule = new FilterRule { IsShow = true };
            rule.GetOrCreateAction("SetBorderColor").Args.AddRange(new[] { "200", "200", "200", "255" });
            rule.GetOrCreateAction("SetTextColor").Args.AddRange(new[] { "255", "255", "255", "255" });
            rule.GetOrCreateAction("SetFontSize").Args.Add("36");
            rule.MarkModified();

            var insertAt = SelectedRule == null ? _document.Nodes.Count : _document.Nodes.IndexOf(SelectedRule.Model) + 1;
            if (insertAt > 0 && _document.Nodes[insertAt - 1] is not BlankNode)
            {
                _document.Nodes.Insert(insertAt, new BlankNode { Count = 1 });
                insertAt++;
            }

            _document.Nodes.Insert(insertAt, rule);
            _document.IsDirty = true;

            RebuildEntries();
            var vm = Entries.OfType<RuleViewModel>().FirstOrDefault(v => ReferenceEquals(v.Model, rule));
            SelectedEntry = vm;
            RebuildPreview();
            Validate();
            StatusMessage = "已添加新规则，请在右侧勾选物品或类别";
        }

        private void DuplicateRule()
        {
            var source = SelectedRule?.Model;
            if (source == null)
            {
                return;
            }

            PushUndo();

            var copy = new FilterRule { IsShow = source.IsShow, Title = source.Title };
            foreach (var condition in source.Conditions)
            {
                copy.Conditions.Add(condition.Clone());
            }

            foreach (var action in source.Actions)
            {
                copy.Actions.Add(action.Clone());
            }

            copy.ExtraLines.AddRange(source.ExtraLines);
            copy.MarkModified();

            var (_, end) = ChunkOf(source);
            var insertAt = end + 1;
            _document.Nodes.Insert(insertAt, new BlankNode { Count = 1 });
            _document.Nodes.Insert(insertAt + 1, copy);
            _document.IsDirty = true;

            RebuildEntries();
            SelectedEntry = Entries.OfType<RuleViewModel>().FirstOrDefault(v => ReferenceEquals(v.Model, copy));
            RebuildPreview();
            Validate();
            StatusMessage = "已复制规则";
        }

        private void DeleteRule()
        {
            var rule = SelectedRule?.Model;
            if (rule == null)
            {
                return;
            }

            PushUndo();

            var (start, end) = ChunkOf(rule);
            _document.Nodes.RemoveRange(start, end - start + 1);
            _document.IsDirty = true;

            RebuildEntries();
            RebuildPreview();
            Validate();
            StatusMessage = "已删除规则";
        }

        private void MoveRule(int direction)
        {
            var rule = SelectedRule?.Model;
            if (rule == null)
            {
                return;
            }

            var (start, end) = ChunkOf(rule);
            var neighbourRule = direction < 0
                ? FindPreviousRule(start)
                : FindNextRule(end);

            if (neighbourRule == null)
            {
                return;
            }

            PushUndo();

            var (nStart, nEnd) = ChunkOf(neighbourRule);
            var currentChunk = _document.Nodes.GetRange(start, end - start + 1);
            var neighbourChunk = _document.Nodes.GetRange(nStart, nEnd - nStart + 1);

            if (direction < 0)
            {
                var between = _document.Nodes.GetRange(nEnd + 1, start - nEnd - 1);
                _document.Nodes.RemoveRange(nStart, end - nStart + 1);
                _document.Nodes.InsertRange(nStart, currentChunk.Concat(between).Concat(neighbourChunk));
            }
            else
            {
                var between = _document.Nodes.GetRange(end + 1, nStart - end - 1);
                _document.Nodes.RemoveRange(start, nEnd - start + 1);
                _document.Nodes.InsertRange(start, neighbourChunk.Concat(between).Concat(currentChunk));
            }

            _document.IsDirty = true;
            RebuildEntries();
            SelectedEntry = Entries.OfType<RuleViewModel>().FirstOrDefault(v => ReferenceEquals(v.Model, rule));
            RebuildPreview();
            Validate();
        }

        private (int Start, int End) ChunkOf(FilterRule rule)
        {
            var end = _document.Nodes.IndexOf(rule);
            var start = end;
            while (start > 0 && _document.Nodes[start - 1] is CommentNode)
            {
                start--;
            }

            return (start, end);
        }

        private FilterRule? FindPreviousRule(int beforeIndex)
        {
            for (var i = beforeIndex - 1; i >= 0; i--)
            {
                if (_document.Nodes[i] is FilterRule rule)
                {
                    return rule;
                }
            }

            return null;
        }

        private FilterRule? FindNextRule(int afterIndex)
        {
            for (var i = afterIndex + 1; i < _document.Nodes.Count; i++)
            {
                if (_document.Nodes[i] is FilterRule rule)
                {
                    return rule;
                }
            }

            return null;
        }

        // ==================== 物品 ====================

        public void AddItemsToSelected(IEnumerable<ItemEntry> items)
        {
            var rule = SelectedRule;
            if (rule == null)
            {
                return;
            }

            rule.AddItems(items);
            StatusMessage = $"已为规则添加 {items.Count()} 个物品";
        }

        public void AddClassesToSelected(IEnumerable<string> classes)
        {
            SelectedRule?.AddClasses(classes);
        }

        public void RemoveItemFromSelected(string nameEn) => SelectedRule?.RemoveItem(nameEn);

        public void RemoveClassFromSelected(string nameEn) => SelectedRule?.RemoveClass(nameEn);

        // ==================== 撤销 / 重做 ====================

        private void PushUndo()
        {
            var text = FilterWriter.Write(_document, false);
            if (_undo.Count > 0 && _undo[^1] == text)
            {
                return;
            }

            _undo.Add(text);
            if (_undo.Count > 60)
            {
                _undo.RemoveAt(0);
            }

            _redo.Clear();
            RaisePropertyChanged(nameof(CanUndo), nameof(CanRedo));
            RaiseCommandStates();
        }

        private void Undo()
        {
            if (_undo.Count == 0)
            {
                return;
            }

            _redo.Add(FilterWriter.Write(_document, false));
            var text = _undo[^1];
            _undo.RemoveAt(_undo.Count - 1);
            ApplyText(text);
            StatusMessage = "已撤销";
        }

        private void Redo()
        {
            if (_redo.Count == 0)
            {
                return;
            }

            _undo.Add(FilterWriter.Write(_document, false));
            var text = _redo[^1];
            _redo.RemoveAt(_redo.Count - 1);
            ApplyText(text);
            StatusMessage = "已重做";
        }

        private void ApplyText(string text)
        {
            var document = FilterParser.Parse(text, _document.FilePath);
            document.HasBom = _document.HasBom;
            document.Encoding = _document.Encoding;
            document.NewLine = _document.NewLine;
            document.EndsWithNewLine = true;
            document.IsDirty = true;
            _document = document;

            RebuildEntries();
            RebuildPreview();
            Validate();
            RaisePropertyChanged(nameof(CanUndo), nameof(CanRedo), nameof(IsDirty), nameof(WindowTitle));
            RaiseCommandStates();
        }

        private void CopyPreview()
        {
            try
            {
                Clipboard.SetText(PreviewText);
                StatusMessage = "过滤器文本已复制到剪贴板";
            }
            catch (Exception ex)
            {
                StatusMessage = $"复制失败：{ex.Message}";
            }
        }

        private void RaiseCommandStates()
        {
            foreach (var command in new[]
                     {
                         DuplicateRuleCommand, DeleteRuleCommand, MoveUpCommand, MoveDownCommand,
                         ToggleDisableCommand, ToggleShowCommand, UndoCommand, RedoCommand, ReloadCommand
                     })
            {
                (command as RelayCommand)?.RaiseCanExecuteChanged();
            }
        }
    }
}
