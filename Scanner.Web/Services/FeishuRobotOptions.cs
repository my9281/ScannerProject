namespace Scanner.Web.Services;

public sealed class FeishuRobotOptions
{
    public const string SectionName = "FeishuRobot";
    public bool Enabled { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string Secret { get; set; } = string.Empty;
    public string PublicBaseUrl { get; set; } = string.Empty;
}

public sealed record FeishuTestRequest(string? Message, string? Url);

