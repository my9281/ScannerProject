using System.Net;
using System.Text.Json.Nodes;
using Scanner.BreezaryStar.CheckBoard.Services;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
foreach (var mode in new[] { "success", "empty", "duplicate", "bad-page", "forbidden", "expired", "not-deployed", "missing-table" })
{
    using var handler = new Server(mode);
    using var http = new HttpClient(handler);
    try
    {
        var rows = await new ForecastClient(http).DownloadAsync(new Uri("https://scanner.example/"), "scanner-session");
        Check(mode is "success" or "empty", "Only valid record responses succeed");
        if (mode == "empty") { Check(rows.Count == 0, "Empty local database"); continue; }
        Check(rows.Count == 2 && handler.Calls == 2, "Read all local record pages");
        var first = rows[0];
        Check(first.Sku == "35015-WG-XL" && first.WorkOrderNumber == "WO-001", "Rich text fields");
        Check(first.TrackingNumber == "ORIGINAL-001" && first.ActualTrackingNumber == "ACTUAL-001", "Preserve separate tracking numbers");
        Check(first.Channel == "UPS" && first.ReceiptStatus == "是" && first.WorkflowStatus == "样机留存", "Option text values");
        Check(first.ShipDate == "2026-10-16" && first.ReceiptDate == "2026-09-30", "Date timestamps formatted consistently with Web");
        Check(first.Remark == "样机" && first.IssueDescription == "测试样机" && first.SyncStatus == "待推送", "Notes, issues and sync state");
    }
    catch (InvalidOperationException ex)
    {
        var expected = mode switch { "duplicate" => "重复", "bad-page" => "分页", "forbidden" => "管理员", "expired" => "失效", "not-deployed" => "部署", "missing-table" => "017", _ => "UNEXPECTED" };
        Check(ex.Message.Contains(expected), "Actionable error for " + mode);
    }
}
using (var http = new HttpClient(new Server("success")))
{
    using var cancel = new CancellationTokenSource(); cancel.Cancel();
    try { await new ForecastClient(http).DownloadAsync(new Uri("https://scanner.example/"), "scanner-session", cancel.Token); throw new Exception("Cancellation ignored"); }
    catch (OperationCanceledException) { checks++; }
}
Console.WriteLine($"Passed {checks} MAUI forecast client checks; no live server or database used.");

sealed class Server(string mode) : HttpMessageHandler
{
    public int Calls;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.Method != HttpMethod.Get || request.RequestUri!.Host != "scanner.example" || request.RequestUri.AbsolutePath != "/api/tencent-docs/records"
            || request.Headers.Authorization?.ToString() != "Bearer scanner-session") throw new Exception("Must only request authenticated Scanner.Web local records");
        Calls++;
        if (mode == "forbidden") return Reply("{}", HttpStatusCode.Forbidden);
        if (mode == "expired") return Reply("{}", HttpStatusCode.Unauthorized);
        if (mode == "not-deployed") return Reply("{}", HttpStatusCode.NotFound);
        if (mode == "missing-table") return Reply("""{"message":"请执行017建表脚本"}""", HttpStatusCode.ServiceUnavailable);
        if (mode == "empty") return Reply("""{"records":[],"hasMore":false}""");
        var row = JsonNode.Parse("""
            {"recordID":"r1","syncStatus":"pending","values":{
            "SKU":[{"format":{},"type":"text","text":"35015-WG-XL"}],"工单号":[{"type":"text","text":"WO-001"}],
            "运单号":[{"text":"ORIGINAL-001"}],"实际签收快递单号":[{"text":"ACTUAL-001"}],"渠道":[{"id":"channel1","text":"UPS"}],
            "是否签收":[{"id":"yes1","text":"是"}],"状态流转":[{"id":"status1","text":"样机留存"}],
            "发货日期":"1792080000000","签收日期":"1790697600000","备注":[{"text":"样机"}],"问题描述":[{"text":"测试样机"}]}}
            """)!;
        bool more = Calls == 1;
        if (!more && mode != "duplicate") row["recordID"] = "r2";
        return Reply(new JsonObject { ["records"] = new JsonArray(row), ["hasMore"] = more, ["next"] = mode == "bad-page" ? 0 : 100 }.ToJsonString());
    }
    private static Task<HttpResponseMessage> Reply(string json, HttpStatusCode status = HttpStatusCode.OK) =>
        Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") });
}
