using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Poe2FilterEditor.Models;
using Poe2FilterEditor.Services;
using Poe2FilterEditor.ViewModels;
using Poe2FilterEditor.Views;

namespace Poe2FilterEditor
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    ShowError(ex);
                }
            };

            // 构建流水线用的无界面自检：依次创建各窗口，验证 XAML 与资源字典可加载。
            if (Array.IndexOf(e.Args, "--ui-smoke") >= 0)
            {
                RunUiSmokeTest();
            }


        }

        private void RunUiSmokeTest()
        {
            var logPath = Path.Combine(Path.GetTempPath(), "poe2-ui-smoke.txt");
            try
            {
                var main = new MainWindow();
                main.Show();
                main.Close();

                var database = ItemDatabase.Load(AppContext.BaseDirectory, null);
                foreach (var mode in new[] { PickerMode.Items, PickerMode.Uniques, PickerMode.Classes })
                {
                    var picker = new ItemPickerWindow(database, mode);
                    picker.Show();
                    picker.Close();
                }

                var colorPicker = new ColorPickerWindow("边框颜色", FilterColor.White);
                colorPicker.Show();
                colorPicker.Close();

                if (main.DataContext is MainViewModel viewModel)
                {
                    var settings = new SettingsWindow(viewModel);
                    settings.Show();
                    settings.Close();
                }

                File.WriteAllText(logPath, $"UI-SMOKE-OK items={database.ItemCount} classes={database.Classes.Count}");
                Shutdown(0);
            }
            catch (Exception ex)
            {
                File.WriteAllText(logPath, "UI-SMOKE-FAIL: " + ex);
                Shutdown(1);
            }
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ShowError(e.Exception);
            e.Handled = true;
        }

        private static void ShowError(Exception ex)
        {
            var sb = new StringBuilder();
            sb.AppendLine("程序遇到了一个未处理的错误：");
            sb.AppendLine();
            sb.AppendLine(ex.Message);
            sb.AppendLine();
            sb.AppendLine(ex.GetType().FullName);
            if (!string.IsNullOrWhiteSpace(ex.StackTrace))
            {
                sb.AppendLine();
                sb.AppendLine(ex.StackTrace);
            }

            MessageBox.Show(sb.ToString(), "出错了", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
