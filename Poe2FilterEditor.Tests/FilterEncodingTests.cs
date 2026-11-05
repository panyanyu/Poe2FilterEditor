using System;
using System.IO;
using System.Linq;
using System.Text;
using Poe2FilterEditor.Services;

namespace Poe2FilterEditor.Tests
{
    /// <summary>
    /// 编码相关的往返测试：BOM、换行、GBK（国服老文件）。
    /// </summary>
    public class FilterEncodingTests
    {
        private const string SampleText =
            "# 神圣石过滤器\r\n\r\nShow\r\n    Class \"Currency\"\r\n    BaseType \"Divine Orb\"\r\n";

        static FilterEncodingTests()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        private static string CreateTempFile()
        {
            var path = Path.Combine(Path.GetTempPath(), "poe2-enc-" + Guid.NewGuid().ToString("N") + ".filter");
            return path;
        }

        [Fact]
        public void GBK文件可正确读取并原样保存()
        {
            var encoding = Encoding.GetEncoding(936);
            var path = CreateTempFile();

            try
            {
                File.WriteAllBytes(path, encoding.GetBytes(SampleText));

                var document = FilterFileService.Load(path);
                var comment = document.Nodes.OfType<Models.CommentNode>().First();
                Assert.Contains("神圣石", comment.Lines[0]);
                Assert.Equal("Divine Orb", document.Rules.Single().FindCondition("BaseType")!.FirstValue);
                Assert.False(document.HasBom);

                FilterFileService.Save(document, path);

                var saved = File.ReadAllBytes(path);
                Assert.Equal(encoding.GetBytes(SampleText), saved);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void UTF16文件保存后保留BOM()
        {
            var encoding = new UnicodeEncoding(false, true);
            var path = CreateTempFile();

            try
            {
                var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(SampleText)).ToArray();
                File.WriteAllBytes(path, bytes);

                var document = FilterFileService.Load(path);
                Assert.True(document.HasBom);
                Assert.Contains("神圣石", document.Nodes.OfType<Models.CommentNode>().First().Lines[0]);

                FilterFileService.Save(document, path);

                var saved = File.ReadAllBytes(path);
                Assert.Equal(new byte[] { 0xFF, 0xFE }, saved.Take(2).ToArray());
                Assert.Equal(bytes, saved);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [Fact]
        public void UTF8BOM文件保存后保留BOM()
        {
            var encoding = new UTF8Encoding(true);
            var path = CreateTempFile();

            try
            {
                var bytes = encoding.GetPreamble().Concat(Encoding.UTF8.GetBytes(SampleText)).ToArray();
                File.WriteAllBytes(path, bytes);

                var document = FilterFileService.Load(path);
                Assert.True(document.HasBom);

                FilterFileService.Save(document, path);

                var saved = File.ReadAllBytes(path);
                Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, saved.Take(3).ToArray());
                Assert.Equal(bytes, saved);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }
    }
}
