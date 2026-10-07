using Scanner.Helpers.Services;
using Scanner.Models;
using System.Net;
using System.IO.Compression;
using System.Xml.Linq;

void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
var time = new DateTime(2026, 10, 7, 14, 23, 45);
var api = new ShelvedPalletApiService(new HttpClient(new Handler()) { BaseAddress = new Uri("https://example.invalid/") });
var pallets = await api.GetPdaPalletsAsync(" test-key ");
Check(pallets.Count == 3 && pallets[0].ScanTime == time, "API date and time");
var inbound = new[] {
    new InboundChecklistRecord { Sn = " 001abc ", Sku = "=SKU", DetectionStatus = "待检测", RmaNumber = "A" },
    new InboundChecklistRecord { Sn = "001ABC", Sku = "SECOND", DetectionStatus = " 待检测 ", RmaNumber = "B" },
    new InboundChecklistRecord { Sn = "DONE", DetectionStatus = "完成" },
    new InboundChecklistRecord { Sn = "MISSING", DetectionStatus = "待检测" },
    new InboundChecklistRecord { Sn = " ", DetectionStatus = "待检测" }
};
var rows = AutomaticMatchingService.Match(pallets, inbound);
Check(rows.Count == 2 && rows.All(x => x.PalletNumber == 2 && x.Sn == "001abc" || x.Sn == "001ABC"), "Pending only, trimmed case-insensitive SN, preserve multiple inbound rows");
var testDirectory = Path.Combine(Directory.GetCurrentDirectory(), ".artifacts", "automatic-matching-tests");
Directory.CreateDirectory(testDirectory);
var path = Path.Combine(testDirectory, "automatic-match-" + Guid.NewGuid().ToString("N") + ".xlsx");
try {
    AutomaticMatchingService.Export(path, rows);
    using var zip = ZipFile.OpenRead(path);
    using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
    var sheet = XDocument.Load(stream); XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    Check(sheet.Descendants(ns + "row").Count() == 3, "Export complete");
    Check(!sheet.Descendants(ns + "f").Any() && sheet.Descendants(ns + "t").Any(x => x.Value == "=SKU"), "Formula-like strings remain text");
    Check(sheet.Descendants(ns + "t").Any(x => x.Value == "001abc") && sheet.Descendants(ns + "t").Any(x => x.Value == "2026-10-07 14:23:45"), "SN and scan timestamp preserved");
} finally { if (File.Exists(path)) File.Delete(path); }
Console.WriteLine("PASS: authenticated PDA GET, dates, pending-only matching, duplicate inbound records, leading-zero SN, Excel export and literal strings.");
var failedApi = new ShelvedPalletApiService(new HttpClient(new FailureHandler()) { BaseAddress = new Uri("https://example.invalid/") });
try { await failedApi.GetPdaPalletsAsync(); throw new Exception("Expected server error"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("服务访问密钥未配置") && !ex.Message.Contains("已部署"), "Preserve real 503 reason"); }
Console.WriteLine("PASS: missing server key 503 displays the actual cause.");

var uploadHandler = new UploadHandler();
var uploadApi = new ShelvedPalletApiService(new HttpClient(uploadHandler) { BaseAddress = new Uri("https://example.invalid/") });
var upload = await uploadApi.UploadMatchedWorkOrdersAsync(rows, " test-key ");
Check(upload.InsertedCount == 2 && upload.Username == "my9281", "Upload result");
await uploadApi.UploadMatchedWorkOrdersAsync(rows, "test-key");
Check(uploadHandler.Keys[0].SequenceEqual(uploadHandler.Keys[1]), "Stable keys on retry");
Check(uploadHandler.Keys[0].Distinct().Count() == 2, "Multiple inbound rows retain separate tasks");
await uploadApi.UploadMatchedWorkOrdersAsync(Enumerable.Repeat(rows[0], 501).ToArray(), "test-key");
Check(uploadHandler.Sizes.Skip(2).SequenceEqual(new[] { 500, 1 }), "Large match is chunked");
Check(uploadHandler.Keys[2].Distinct().Count() == 500, "Identical inbound records retain occurrence identity");
try { await failedApi.UploadMatchedWorkOrdersAsync(rows); throw new Exception("Expected upload failure"); }
catch (InvalidOperationException ex) { Check(ex.Message.Contains("服务访问密钥未配置"), "Upload error preserved"); }
Console.WriteLine("PASS: work order mapping, authenticated batch upload, stable retry identity, duplicate records, 500-row chunks and server errors.");

sealed class UploadHandler : HttpMessageHandler
{
    public List<string[]> Keys { get; } = new();
    public List<int> Sizes { get; } = new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Post || request.RequestUri!.AbsolutePath != "/api/checklist-work-orders/batch" || request.Headers.GetValues("X-Upload-Key").Single() != "test-key") throw new Exception("Wrong upload contract");
        var payload = Newtonsoft.Json.Linq.JObject.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
        var items = (Newtonsoft.Json.Linq.JArray)payload["items"]!;
        Keys.Add(items.Select(x => (string)x["sourceKey"]!).ToArray());
        Sizes.Add(items.Count);
        if (items.Any(x => string.IsNullOrWhiteSpace((string)x["deviceId"]) || !((string)x["description"]!).Contains("托盘号：2") || x["userId"] != null || x["auditStatus"] != null)) throw new Exception("Wrong work order mapping");
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent($"{{\"insertedCount\":{items.Count},\"skippedCount\":0,\"userId\":8,\"username\":\"my9281\"}}") };
    }
}

sealed class FailureHandler : HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { Content = new StringContent("{\"message\":\"服务访问密钥未配置。\"}") });
}

sealed class Handler : HttpMessageHandler {
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
        if (request.Method != HttpMethod.Get || request.RequestUri!.AbsolutePath != "/api/tester-pallet-scans" || request.Headers.GetValues("X-Upload-Key").Single() != "test-key") throw new Exception("Wrong API contract");
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("[{\"sn\":\"001ABC\",\"palletNumber\":2,\"scanDate\":\"2026-10-07\",\"scanTime\":\"2026-10-07T14:23:45\"},{\"sn\":\"DONE\",\"palletNumber\":1},{\"sn\":\"UNMATCHED\",\"palletNumber\":3}]") });
    }
}
