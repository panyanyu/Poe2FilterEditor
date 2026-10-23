using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using Poe2FilterEditor.Services;
using Poe2FilterEditor.ViewModels;

namespace Poe2FilterEditor.Views
{
    public partial class SettingsWindow : Window
    {
        private readonly MainViewModel _vm;

        public SettingsWindow(MainViewModel vm)
        {
            InitializeComponent();
            Infrastructure.WindowChromeHelper.ApplyDarkTitleBar(this);

            _vm = vm;
            DataContext = vm;
        }

        private void OnBrowseSoundRoot(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "选择音效根目录"
            };

            if (dialog.ShowDialog() == true)
            {
                _vm.SoundRootDirectory = dialog.FolderName;
            }
        }

        private void OnImportMapping(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "导入映射 JSON",
                Filter = "映射文件 (*.json)|*.json|所有文件 (*.*)|*.*",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                _vm.ImportMapping(dialog.FileName);
            }
        }

        private void OnOpenMappingFolder(object sender, RoutedEventArgs e)
        {
            var path = _vm.Database.UserFilePath;
            var directory = string.IsNullOrEmpty(path) ? SettingsService.AppDataDirectory : Path.GetDirectoryName(path);

            try
            {
                Directory.CreateDirectory(directory!);
                Process.Start(new ProcessStartInfo { FileName = directory!, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"无法打开目录：{ex.Message}", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnClose(object sender, RoutedEventArgs e) => Close();
    }
}
