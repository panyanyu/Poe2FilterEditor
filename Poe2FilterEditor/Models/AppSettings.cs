namespace Poe2FilterEditor.Models
{
    /// <summary>
    /// 用户设置，保存在 %APPDATA%\Poe2FilterEditor\settings.json。
    /// </summary>
    public sealed class AppSettings
    {
        public string? LastFilePath { get; set; }

        public string? LastDirectory { get; set; }

        /// <summary>音效文件的根目录，默认使用过滤器所在目录。</summary>
        public string? SoundRootDirectory { get; set; }

        /// <summary>界面是否显示国服中文名。</summary>
        public bool ShowChineseNames { get; set; } = true;

        /// <summary>保存时把 == / &gt;= 等非标准写法归一化为隐式写法。</summary>
        public bool NormalizeOperatorsOnSave { get; set; }

        public double WindowWidth { get; set; } = 1440;

        public double WindowHeight { get; set; } = 900;

        public bool PreviewCollapsed { get; set; }

        public double RulesPaneWidth { get; set; } = 310;

        public double PreviewPaneWidth { get; set; } = 430;

        /// <summary>用户自定义映射文件路径（默认位于设置目录）。</summary>
        public string? UserMappingFile { get; set; }
    }
}
