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
    /// <summary>文档列表、预览、规则编辑与撤销重做。</summary>
    public sealed partial class MainViewModel
    {
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

            var insertAt = _document.Nodes.IndexOf(source) + 1;
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

            // 只带走这条规则自己的标题行，保留上方的分组注释（例如「# 通货」）
            TakeTitleLine(rule);
            _document.Nodes.Remove(rule);
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

            var index = _document.Nodes.IndexOf(rule);
            var neighbourRule = direction < 0
                ? FindPreviousRule(index)
                : FindNextRule(index);

            if (neighbourRule == null)
            {
                return;
            }

            PushUndo();

            // 两条规则各自带走自己的标题行，交换位置后注释不会错位
            var title = TakeTitleLine(rule);
            var neighbourTitle = TakeTitleLine(neighbourRule);

            var ruleIndex = _document.Nodes.IndexOf(rule);
            var neighbourIndex = _document.Nodes.IndexOf(neighbourRule);
            _document.Nodes[ruleIndex] = neighbourRule;
            _document.Nodes[neighbourIndex] = rule;

            InsertTitleLine(_document.Nodes.IndexOf(rule), title);
            InsertTitleLine(_document.Nodes.IndexOf(neighbourRule), neighbourTitle);

            _document.IsDirty = true;
            RebuildEntries();
            SelectedEntry = Entries.OfType<RuleViewModel>().FirstOrDefault(v => ReferenceEquals(v.Model, rule));
            RebuildPreview();
            Validate();
        }

        /// <summary>
        /// 取出规则正上方的标题行（只取最后一行，保留同块里的分组注释），没有则返回 null。
        /// </summary>
        private string? TakeTitleLine(FilterRule rule)
        {
            var nodes = _document.Nodes;
            var index = nodes.IndexOf(rule);
            if (index <= 0 || nodes[index - 1] is not CommentNode comment || comment.Lines.Count == 0)
            {
                return null;
            }

            var line = comment.Lines[^1];
            var text = line.TrimStart('#').Trim();
            if (text.Length == 0 || text.All(ch => ch == '=' || ch == '-' || ch == '*'))
            {
                return null;
            }

            comment.Lines.RemoveAt(comment.Lines.Count - 1);
            if (comment.Lines.Count == 0)
            {
                nodes.RemoveAt(index - 1);
            }

            return line;
        }

        private void InsertTitleLine(int index, string? titleLine)
        {
            if (string.IsNullOrEmpty(titleLine))
            {
                return;
            }

            var comment = new CommentNode();
            comment.Lines.Add(titleLine);
            _document.Nodes.Insert(index, comment);
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

        public void AddUniquesToSelected(IEnumerable<UniqueEntry> uniques)
        {
            var rule = SelectedRule;
            if (rule == null)
            {
                return;
            }

            var list = uniques.ToList();
            if (list.Count == 0)
            {
                return;
            }

            rule.AddUniques(list);

            var baseCount = list.Select(u => u.BaseEn).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            StatusMessage = $"已按 {list.Count} 件传奇添加筛选：{baseCount} 个基底（同基底的其它传奇也会命中）";
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
