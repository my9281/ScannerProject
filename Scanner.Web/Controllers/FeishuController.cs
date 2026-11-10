using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Filters;
using Scanner.Web.Services;
using Scanner.Server.BLL;
using Scanner.Server.Model;
using Microsoft.Extensions.Options;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/feishu")]
[ApiKey]
public sealed class FeishuController(
    IFeishuRobotService feishu,
    IShelvedPalletService pallets,
    IOptions<FeishuRobotOptions> options) : ControllerBase
{
    [HttpPost("pallet")]
    public async Task<IActionResult> PushPallet(
        [FromQuery] DateOnly t,
        [FromQuery] string p,
        CancellationToken cancellationToken)
    {
        if (t == default) return BadRequest(new { sent = false, message = "上架日期 t 不能为空。" });
        if (string.IsNullOrWhiteSpace(p)) return BadRequest(new { sent = false, message = "托盘号 p 不能为空。" });

        DateTime from = t.ToDateTime(TimeOnly.MinValue);
        ShelvedPalletTableResult table = await pallets.GetAllAsync(cancellationToken);
        IReadOnlyList<IReadOnlyDictionary<string, object?>> matchedRows = table.Rows.Where(row =>
            DateMatches(row, t, "shelving_date", "shelvingDate")
            && string.Equals(TextValue(row, "pallet_number", "palletNumber"), p.Trim(), StringComparison.Ordinal)).ToArray();
        int count = matchedRows.Count;
        if (count == 0) return NotFound(new { sent = false, message = "没有找到对应的上架托盘数据。" });
        FeishuSkuItem[] skuItems = matchedRows
            .Select(row => TextValue(row, "sku", "SKU"))
            .Where(sku => !string.IsNullOrWhiteSpace(sku))
            .GroupBy(sku => sku.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new FeishuSkuItem(group.Key, group.Count()))
            .OrderBy(item => item.Sku, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        string baseUrl = string.IsNullOrWhiteSpace(options.Value.PublicBaseUrl)
            ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}"
            : options.Value.PublicBaseUrl.TrimEnd('/');
        string pageUrl = $"{baseUrl}/pallet-data.html?t={t:yyyy-MM-dd}&p={Uri.EscapeDataString(p.Trim())}";
        bool sent = await feishu.SendShelvedPalletAsync(p.Trim(), from, count, skuItems, pageUrl, cancellationToken);
        return sent ? Ok(new { sent = true, message = "已推送到飞书。" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { sent = false, message = "飞书推送失败，请检查机器人配置和服务器日志。" });
    }

    private static bool DateMatches(IReadOnlyDictionary<string, object?> row, DateOnly expected, params string[] names)
    {
        foreach (string name in names)
        {
            if (!row.TryGetValue(name, out object? value) || value is null) continue;
            if (value is DateTime dateTime) return DateOnly.FromDateTime(dateTime) == expected;
            if (value is DateOnly dateOnly) return dateOnly == expected;
            string text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            if (DateOnly.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateOnly parsed)) return parsed == expected;
            if (DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateTime)) return DateOnly.FromDateTime(dateTime) == expected;
        }
        return false;
    }

    private static string TextValue(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        foreach (string name in names)
            if (row.TryGetValue(name, out object? value)) return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return string.Empty;
    }

    [HttpPost("test")]
    public async Task<IActionResult> Test([FromBody] FeishuTestRequest? request, CancellationToken cancellationToken)
    {
        bool sent = await feishu.SendTestAsync(request?.Message ?? "飞书机器人连接测试成功。", request?.Url, cancellationToken);
        return sent ? Ok(new { sent = true, message = "飞书消息发送成功。" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { sent = false, message = "飞书机器人未启用、配置错误或推送失败，请查看服务器日志。" });
    }
}
