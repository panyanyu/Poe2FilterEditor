using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Poe2FilterEditor.Models;
using Poe2FilterEditor.Services;
using Poe2FilterEditor.ViewModels;

namespace Poe2FilterEditor.Views
{
    public partial class ItemPickerWindow : Window
    {
        private readonly ItemPickerViewModel _vm;

        public ItemPickerWindow(ItemDatabase database, PickerMode mode)
        {
            InitializeComponent();
            Infrastructure.WindowChromeHelper.ApplyDarkTitleBar(this);

            _vm = new ItemPickerViewModel(database, mode);
            DataContext = _vm;
        }

        /// <summary>打开物品选择器，返回勾选的物品；取消时返回 null。</summary>
        public static IReadOnlyList<ItemEntry>? PickItems(Window owner, ItemDatabase database)
        {
            var window = new ItemPickerWindow(database, PickerMode.Items) { Owner = owner };
            return window.ShowDialog() == true ? window._vm.PickedItems : null;
        }

        /// <summary>打开类别选择器，返回勾选的类别英文名；取消时返回 null。</summary>
        public static IReadOnlyList<string>? PickClasses(Window owner, ItemDatabase database)
        {
            var window = new ItemPickerWindow(database, PickerMode.Classes) { Owner = owner };
            return window.ShowDialog() == true ? window._vm.PickedClasses : null;
        }

        private void OnConfirm(object sender, RoutedEventArgs e)
        {
            var count = _vm.Mode == PickerMode.Items ? _vm.PickedItems.Count : _vm.PickedClasses.Count;
            if (count == 0)
            {
                MessageBox.Show("请至少勾选一项。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            DialogResult = true;
        }

        private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;

        private void OnRowDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (RowsList.SelectedItem is ItemPickRow row)
            {
                row.IsSelected = !row.IsSelected;
            }
        }
    }
}
