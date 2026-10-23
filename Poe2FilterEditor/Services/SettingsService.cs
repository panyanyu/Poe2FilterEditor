using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Poe2FilterEditor.Models;

namespace Poe2FilterEditor.Services
{
    /// <summary>
    /// 用户设置与应用数据目录（%APPDATA%\Poe2FilterEditor）。
    /// </summary>
    public static class SettingsService
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string AppDataDirectory { get; } = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Poe2FilterEditor");

        public static string SettingsFile => Path.Combine(AppDataDirectory, "settings.json");

        public static string DefaultUserMappingFile => Path.Combine(AppDataDirectory, "items.user.json");

        public static AppSettings Load()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile, Encoding.UTF8);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json, Options);
                    if (settings != null)
                    {
                        return settings;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"读取设置失败：{ex.Message}");
            }

            return new AppSettings();
        }

        public static void Save(AppSettings settings)
        {
            try
            {
                Directory.CreateDirectory(AppDataDirectory);
                File.WriteAllText(SettingsFile, JsonSerializer.Serialize(settings, Options), new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"保存设置失败：{ex.Message}");
            }
        }
    }
}
