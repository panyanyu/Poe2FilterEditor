namespace Poe2FilterEditor.Models
{
    public enum IssueSeverity
    {
        Info,
        Warning,
        Error
    }

    /// <summary>
    /// 校验问题条目。
    /// </summary>
    public sealed class ValidationIssue
    {
        public ValidationIssue(IssueSeverity severity, string message, FilterRule? rule = null, string? detail = null)
        {
            Severity = severity;
            Message = message;
            Rule = rule;
            Detail = detail;
        }

        public IssueSeverity Severity { get; }

        public string Message { get; }

        public string? Detail { get; }

        public FilterRule? Rule { get; }

        public int LineNumber => Rule?.LineNumber ?? 0;

        public string SeverityText => Severity switch
        {
            IssueSeverity.Error => "错误",
            IssueSeverity.Warning => "警告",
            _ => "提示"
        };

        public string SeverityColor => Severity switch
        {
            IssueSeverity.Error => "#E0645A",
            IssueSeverity.Warning => "#E29A4A",
            _ => "#7FA9C9"
        };
    }
}
