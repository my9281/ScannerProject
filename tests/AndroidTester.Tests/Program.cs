using Scanner.AndroidTester.Services;
void Check(bool value, string message) { if (!value) throw new Exception(message); }
var session = new ScanSession();
Check(!session.AddPreScan(" \r\n"), "Blank pre-scan ignored");
session.AddPreScan("00123\r\n"); session.AddPreScan("00123");
Check(session.PreScans.Count == 1 && session.PreScans[0] == "00123", "Deduplicate and preserve leading zeroes");
Check(session.LastScan is null, "Pre-scan must not update last scan");
var result = session.Detect("00123")!;
Check(!result.SameAsLast && result.InPreScanList, "First list hit");
result = session.Detect("00123")!;
Check(result.SameAsLast && result.InPreScanList, "Both alerts must fire");
result = session.Detect("OTHER")!;
Check(!result.SameAsLast && !result.InPreScanList && session.LastScan == "OTHER", "Update last scan even without alert");
Check(session.Detect(" \r\n") is null && session.LastScan == "OTHER", "Blank must not replace last scan");
result = session.Detect("OTHER")!;
Check(result.SameAsLast && !result.InPreScanList, "Same-only alert");
result = session.Detect("other")!;
Check(!result.SameAsLast, "Exact case-sensitive comparison");
Check(new ScanSession().PreScans.Count == 0 && new ScanSession().LastScan is null, "New session starts empty");
var latest = new LatestScan();
latest.Record(" 000123\r\n");
latest.Record(" ");
Check(latest.Value == "000123", "Printable latest scan preserves zeroes and ignores empty input");
latest.Record("NEXT");
Check(latest.Value == "NEXT", "Latest printable scan updates");
Console.WriteLine("PASS: pre-scan deduplication, both alerts, last-scan updates, blank input, exact comparison, session reset and printable latest scan.");
var settings = AppSettings.Load();
Check(settings.UploadBaseUrl.IsAbsoluteUri && settings.UploadApiKey.Length > 0, "Embedded XML settings load");
using (var xml = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<configuration><appSettings><add key='UploadBaseUrl' value='https://example.invalid/api'/><add key='UploadApiKey' value=' test&amp;key '/></appSettings></configuration>")))
{
    var parsed = AppSettings.Parse(xml);
    Check(parsed.UploadBaseUrl.AbsoluteUri == "https://example.invalid/api/" && parsed.UploadApiKey == "test&key", "URL normalization and XML escaping");
}
Console.WriteLine("PASS: embedded XML configuration, server address and upload key parsing.");
var pairs = new SkuSnSession();
pairs.Scan(" SKU-01 ");
Check(pairs.PendingSku == "SKU-01" && pairs.Items.Count == 0, "First scan is SKU only");
pairs.Scan("000123");
Check(pairs.PendingSku == null && pairs.Items[0].Sn == "000123", "Second scan completes pair");
pairs.Scan("SKU-02");
try { pairs.Scan("000123"); throw new Exception("Duplicate accepted"); } catch (ArgumentException) { }
pairs.ResetPair();
var batchGuid = pairs.BatchId;
var pairHandler = new PairHandler();
using var pairClient = new HttpClient(pairHandler) { BaseAddress = new Uri("https://example.invalid/") };
var pairSettings = new AppSettings(pairClient.BaseAddress, "test");
try { await pairs.UploadAsync(pairClient, pairSettings); throw new Exception("Failure expected"); } catch (InvalidOperationException) { }
Check(pairs.BatchId == batchGuid && pairs.Items.Count == 1, "Failed batch retained");
try { pairs.Scan("NEW"); throw new Exception("Sealed batch changed"); } catch (ArgumentException) { }
pairHandler.Success = true;
await pairs.UploadAsync(pairClient, pairSettings);
Check(pairs.Items.Count == 0 && pairs.BatchId != batchGuid && pairHandler.Seen.All(x => x == batchGuid), "Retry identity and new batch");
Console.WriteLine("PASS: SKU/SN pairing, duplicates, server validation, frozen retry and new batch GUID.");
var cachePath = Path.Combine(AppContext.BaseDirectory, "sku-sn-test-" + Guid.NewGuid() + ".json");
try
{
    var durable = new SkuSnSession(cachePath); durable.Scan("SKU");
    var restored = new SkuSnSession(cachePath);
    Check(restored.PendingSku == "SKU", "Restore half pair");
    restored.Scan("000001");
    var complete = new SkuSnSession(cachePath);
    Check(complete.Items.Count == 1 && complete.BatchId == restored.BatchId, "Restore completed batch");
}
finally { File.Delete(cachePath); File.Delete(cachePath + ".tmp"); }
Console.WriteLine("PASS: durable batch and half-pair recovery.");
sealed class PairHandler : HttpMessageHandler
{
    public bool Success;
    public List<Guid> Seen = new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var batch = (await System.Net.Http.Json.HttpContentJsonExtensions.ReadFromJsonAsync<Scanner.Server.Model.SkuSnBatch>(request.Content!,ct))!;
        if (request.RequestUri!.AbsolutePath != "/api/sku-sn-batches" || request.Headers.GetValues("X-Upload-Key").Single() != "test") throw new Exception("Route/key mismatch");
        batch = Scanner.Server.BLL.SkuSnBatchValidator.Validate(batch);
        Seen.Add(batch.BatchId);
        return Success ? new(System.Net.HttpStatusCode.OK) { Content = System.Net.Http.Json.JsonContent.Create(new { batch.BatchId, ItemCount = batch.Items.Count }) } : new(System.Net.HttpStatusCode.BadGateway);
    }
}
