using System;
using System.IO;
using System.Text;
using Poe2FilterEditor.Models;

namespace Poe2FilterEditor.Services
{
    /// <summary>
    /// 过滤器文件的读写：保留 BOM / 换行风格，保存时先写临时文件再原子替换并生成 .bak 备份。
    /// </summary>
    public static class FilterFileService
    {
        public static FilterDocument Load(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var (text, encoding, hasBom) = Decode(bytes);

            var document = FilterParser.Parse(text, path);
            document.Encoding = encoding;
            document.HasBom = hasBom;
            return document;
        }

        private static (string Text, Encoding Encoding, bool HasBom) Decode(byte[] bytes)
        {
            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return (Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), new UTF8Encoding(true), true);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
            {
                return (Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), new UnicodeEncoding(false, false), true);
            }

            if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
            {
                return (Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), new UnicodeEncoding(true, false), true);
            }

            // 无 BOM：先按严格 UTF-8 解码，失败再用系统 ANSI（国服老文件可能是 GBK）
            try
            {
                var strict = new UTF8Encoding(false, true);
                return (strict.GetString(bytes), new UTF8Encoding(false), false);
            }
            catch (DecoderFallbackException)
            {
                try
                {
                    var ansi = Encoding.GetEncoding(936);
                    return (ansi.GetString(bytes), ansi, false);
                }
                catch
                {
                    return (Encoding.UTF8.GetString(bytes), new UTF8Encoding(false), false);
                }
            }
        }

        public static void Save(FilterDocument document, string path, bool normalizeOperators = false)
        {
            var text = FilterWriter.Write(document, normalizeOperators);
            var encoding = document.Encoding ?? new UTF8Encoding(false);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var preamble = document.HasBom ? encoding.GetPreamble() : Array.Empty<byte>();
            var body = encoding.GetBytes(text);
            var payload = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, payload, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, payload, preamble.Length, body.Length);

            var tempPath = path + ".tmp";
            File.WriteAllBytes(tempPath, payload);

            if (File.Exists(path))
            {
                try
                {
                    File.Copy(path, path + ".bak", true);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"备份失败：{ex.Message}");
                }
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Replace(tempPath, path, null, true);
                }
                else
                {
                    File.Move(tempPath, path);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"原子替换失败，回退为直接写入：{ex.Message}");
                File.Copy(tempPath, path, true);
                File.Delete(tempPath);
            }

            document.FilePath = path;
            document.IsDirty = false;

            // 只刷新真正被重排过的规则，未修改的规则继续沿用原始文本
            foreach (var rule in document.Rules)
            {
                if (!FilterWriter.NeedsRegenerate(rule, normalizeOperators))
                {
                    continue;
                }

                var lines = FilterWriter.WriteRule(rule, normalizeOperators);
                rule.OriginalLines.Clear();
                rule.OriginalLines.AddRange(lines);
                rule.IsModified = false;
            }
        }
    }
}
