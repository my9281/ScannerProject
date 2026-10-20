using System.Globalization;
using System.Text.Json.Nodes;

namespace Scanner.BreezaryStar.CheckBoard.Models;

public sealed record ForecastRow(string RecordId, string Sku, string WorkOrderNumber, string TrackingNumber,
    string ActualTrackingNumber, string Channel, string ReceiptStatus, string WorkflowStatus,
    string ShipDate, string ReceiptDate, string Remark, string IssueDescription, string SyncStatus)
{
    public string Heading => $"{WorkOrderNumber}  ·  {Sku}";
    public string Transport => $"渠道：{Channel}    运单号：{TrackingNumber}\n实际签收快递单号：{ActualTrackingNumber}";
    public string Status => $"是否签收：{ReceiptStatus}    状态流转：{WorkflowStatus}";
    public string Dates => $"发货日期：{ShipDate}    签收日期：{ReceiptDate}";
    public string Notes => $"备注：{Remark}\n问题描述：{IssueDescription}";

    public static ForecastRow Parse(JsonNode record)
    {
        var id = record["recordID"]?.ToString();
        if (string.IsNullOrWhiteSpace(id) || record["values"] is not JsonObject values)
            throw new InvalidOperationException("服务器返回的预报记录缺少记录 ID 或字段值。");
        string Get(string name) => Text(values[name]);
        return new(id, Get("SKU"), Get("工单号"), Get("运单号"), Get("实际签收快递单号"), Get("渠道"),
            Get("是否签收"), Get("状态流转"), Date(Get("发货日期")), Date(Get("签收日期")), Get("备注"), Get("问题描述"),
            record["syncStatus"]?.ToString() switch { "clean" => "已同步", "pending" => "待推送", "error" => "推送失败", "conflict" => "存在冲突", _ => "—" });
    }

    private static string Text(JsonNode? value)
    {
        if (value is JsonArray array) return string.Join(" / ", array.Select(Text));
        if (value is JsonObject obj)
            foreach (var key in new[] { "text", "label", "name", "value" })
                if (obj.ContainsKey(key)) return Text(obj[key]);
        return value?.ToString() ?? "";
    }

    private static string Date(string value)
    {
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var milliseconds)) return value;
        try { return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).ToOffset(TimeSpan.FromHours(8)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture); }
        catch (ArgumentOutOfRangeException) { return value; }
    }
}
