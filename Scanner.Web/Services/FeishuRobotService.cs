using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Scanner.Web.Services;

public sealed class FeishuRobotService(
    HttpClient httpClient,
    IOptions<FeishuRobotOptions> options,
    ILogger<FeishuRobotService> logger) : IFeishuRobotService
{
    private readonly FeishuRobotOptions _options = options.Value;

    public Task<bool> SendShelvedPalletAsync(string palletNumber, DateTime shelvedAt, int count, IReadOnlyList<FeishuSkuItem> skuItems, string pageUrl, CancellationToken cancellationToken = default)
    {
        string table = BuildSkuTable(skuItems);
        string content = $"**托盘号：** {Escape(palletNumber)}\n**上架日期：** {shelvedAt:yyyy-MM-dd}\n**数据数量：** {count}\n**SKU 种类：** {skuItems.Count}\n\n{table}\n[查看完整 SKU / SN 明细]({pageUrl})";
        return SendCardAsync("上架托盘数据已上传", content, cancellationToken);
    }

    public Task<bool> SendTestAsync(string message, string? url, CancellationToken cancellationToken = default)
    {
        string content = Escape(string.IsNullOrWhiteSpace(message) ? "飞书机器人连接测试成功。" : message.Trim());
        if (!string.IsNullOrWhiteSpace(url)) content += $"\n[打开链接]({url.Trim()})";
        return SendCardAsync("Web 后台推送测试", content, cancellationToken);
    }

    private async Task<bool> SendCardAsync(string title, string content, CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation("Feishu robot notification skipped because it is disabled.");
            return false;
        }
        if (!Uri.TryCreate(_options.WebhookUrl, UriKind.Absolute, out Uri? webhook))
        {
            logger.LogError("Feishu robot webhook is not configured or invalid.");
            return false;
        }

        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        Dictionary<string, object?> payload = new()
        {
            ["msg_type"] = "interactive",
            ["card"] = new
            {
                config = new { wide_screen_mode = true },
                header = new { template = "blue", title = new { tag = "plain_text", content = title } },
                elements = new object[] { new { tag = "markdown", content } }
            }
        };
        if (!string.IsNullOrWhiteSpace(_options.Secret))
        {
            payload["timestamp"] = timestamp.ToString();
            payload["sign"] = Sign(timestamp, _options.Secret.Trim());
        }

        try
        {
            using StringContent body = new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using HttpResponseMessage response = await httpClient.PostAsync(webhook, body, cancellationToken);
            string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("Feishu robot returned HTTP {StatusCode}: {Response}", (int)response.StatusCode, responseBody);
                return false;
            }
            using JsonDocument json = JsonDocument.Parse(responseBody);
            int code = json.RootElement.TryGetProperty("code", out JsonElement value) && value.TryGetInt32(out int parsed) ? parsed : 0;
            if (code != 0)
            {
                logger.LogError("Feishu robot rejected the message: {Response}", responseBody);
                return false;
            }
            return true;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Unable to send Feishu robot notification.");
            return false;
        }
    }

    private static string Sign(long timestamp, string secret)
    {
        byte[] key = Encoding.UTF8.GetBytes($"{timestamp}\n{secret}");
        using HMACSHA256 hmac = new(key);
        return Convert.ToBase64String(hmac.ComputeHash(Array.Empty<byte>()));
    }

    private static string BuildSkuTable(IReadOnlyList<FeishuSkuItem> items)
    {
        if (items.Count == 0) return "**SKU 汇总**\n暂无 SKU 数据\n";
        const int maxRows = 30;
        IEnumerable<FeishuSkuItem> visible = items.Take(maxRows);
        StringBuilder table = new("**SKU 汇总**\n```\nSKU                                      数量\n");
        table.AppendLine("---------------------------------------- ----");
        foreach (FeishuSkuItem item in visible)
        {
            string sku = (item.Sku ?? string.Empty).Replace("`", "'");
            if (sku.Length > 40) sku = sku[..37] + "...";
            table.Append(sku.PadRight(40)).Append(' ').AppendLine(item.Quantity.ToString());
        }
        table.Append("```\n");
        if (items.Count > maxRows) table.Append($"其余 {items.Count - maxRows} 种 SKU 请打开明细页面查看。\n");
        return table.ToString();
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("*", "\\*").Replace("[", "\\[").Replace("]", "\\]");
}
