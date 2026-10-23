using System;
using System.IO;
using System.Linq;

namespace Poe2FilterEditor.Services
{
    /// <summary>
    /// 音效路径解析与检查。过滤器里的路径是相对客户端目录的，因此可能无法在本地校验。
    /// </summary>
    public static class SoundService
    {
        public static readonly string[] SupportedExtensions = { ".mp3", ".wav", ".ogg" };

        public static string SuggestPath(string chineseName) =>
            $"sounds/{chineseName}{SupportedExtensions[0]}";

        /// <summary>返回解析到的本地绝对路径（可能不存在）。</summary>
        public static string? Resolve(string? soundPath, string? filterPath, string? soundRoot)
        {
            if (string.IsNullOrWhiteSpace(soundPath))
            {
                return null;
            }

            var normalized = soundPath.Replace('\\', '/').Trim();
            var candidates = new System.Collections.Generic.List<string>();

            if (Path.IsPathRooted(normalized))
            {
                candidates.Add(normalized);
            }

            if (!string.IsNullOrWhiteSpace(soundRoot))
            {
                candidates.Add(Path.Combine(soundRoot!, normalized));
            }

            if (!string.IsNullOrWhiteSpace(filterPath))
            {
                var filterDirectory = Path.GetDirectoryName(filterPath);
                if (!string.IsNullOrEmpty(filterDirectory))
                {
                    candidates.Add(Path.Combine(filterDirectory, normalized));
                    candidates.Add(Path.Combine(filterDirectory, "sounds", Path.GetFileName(normalized)));
                }
            }

            candidates.Add(Path.Combine(AppContext.BaseDirectory, normalized));

            return candidates.FirstOrDefault(File.Exists) ?? candidates.FirstOrDefault();
        }

        public static bool Exists(string? soundPath, string? filterPath, string? soundRoot)
        {
            var resolved = Resolve(soundPath, filterPath, soundRoot);
            return resolved != null && File.Exists(resolved);
        }

        public static bool HasKnownExtension(string? soundPath)
        {
            if (string.IsNullOrWhiteSpace(soundPath))
            {
                return false;
            }

            var extension = Path.GetExtension(soundPath).ToLowerInvariant();
            return SupportedExtensions.Contains(extension);
        }

        public static string? PickFolder(string? initialDirectory)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "选择音效文件",
                Filter = "音效文件|*.mp3;*.wav;*.ogg|所有文件|*.*",
                CheckFileExists = true
            };

            if (!string.IsNullOrWhiteSpace(initialDirectory) && Directory.Exists(initialDirectory))
            {
                dialog.InitialDirectory = initialDirectory;
            }

            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
