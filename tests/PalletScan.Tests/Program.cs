using System.Net;
using System.Net.Http.Json;
using Scanner.AndroidTester.Services;
using Scanner.Server.Model;
using Scanner.Server.BLL;
using Scanner.Server.DAL;

void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
async Task Reject(Func<Task> action) { try { await action(); } catch (ArgumentException) { return; } throw new Exception("Expected validation failure"); }
var repository = new FakeRepository();
var service = new PalletScanService(repository);
var time = new DateTimeOffset(2026, 10, 3, 23, 59, 59, TimeSpan.FromHours(-7)).AddTicks(1234567);
var valid = new PalletScanItem(Guid.NewGuid(), " 000123 ", 100, time);
await service.UploadAsync(new(new[] { valid }));
Check(repository.Last![0].Sn == "000123" && repository.Last[0].ScannedAt.Date == time.Date && repository.Last[0].ScannedAt.Offset == time.Offset, "SN/date/offset preservation");
Check(repository.Last[0].ScannedAt.Ticks % 10000 == 0, "Millisecond precision");
int calls = repository.Calls;
foreach (var bad in new[] { valid with { PalletNumber = 0 }, valid with { PalletNumber = 101 }, valid with { Sn = " " }, valid with { Sn = new string('X', 101) }, valid with { ScanId = Guid.Empty }, valid with { ScannedAt = default } })
    await Reject(() => service.UploadAsync(new(new[] { bad })));
await Reject(() => service.UploadAsync(new(new[] { valid, valid })));
await Reject(() => service.UploadAsync(new(Array.Empty<PalletScanItem>())));
await Reject(() => service.UploadAsync(new(Enumerable.Range(0, 1001).Select(_ => valid with { ScanId = Guid.NewGuid() }).ToArray())));
Check(repository.Calls == calls, "Invalid batch must not reach repository");

var session = new PalletScanSession { PalletNumber = 1, ApiKey = "test-key" };
foreach (var badBarcode in new[] { "4G/Y19/S/25CN/C520203", "SN--123", "SN-A-123", "SN/A/123" })
    await Reject(() => { session.Add(badBarcode, time); return Task.CompletedTask; });
Check(session.Pending.Count == 0, "Invalid label barcodes must not enter pending records");
var allowedSeparators = new PalletScanSession();
allowedSeparators.Add("SN/A-123", time);
Check(allowedSeparators.Pending.Count == 1, "One slash and one hyphen remain valid");
foreach (var number in new[] { 0, 101 }) { session.PalletNumber = number; await Reject(() => { session.Add("INVALID", time); return Task.CompletedTask; }); }
session.PalletNumber = 1;
session.Add("000123", time);
session.PalletNumber = 100;
try { session.Add("000123", time.AddSeconds(1)); throw new Exception("Duplicate accepted"); } catch (ArgumentException) { }
session.Add("000124", time.AddSeconds(1));
Check(session.Pending[0].PalletNumber == 1 && session.Pending[1].PalletNumber == 100 && session.Pending[0].ScanId != session.Pending[1].ScanId, "Snapshot pallet and preserve repeated scans");
Guid[] original = session.Pending.Select(x => x.ScanId).ToArray();
var handler = new FakeHandler();
var api = new PalletScanApiService(new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") });
handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
try { await session.UploadAsync(api); throw new Exception("Failure expected"); } catch (InvalidOperationException) { }
Check(!session.IsUploading && session.Pending.Select(x => x.ScanId).SequenceEqual(original), "Failure retains same IDs");
handler.Respond = async (request, ct) =>
{
    Check(request.RequestUri!.AbsolutePath == "/api/tester-pallet-scans", "Route");
    Check(request.Headers.GetValues("X-Upload-Key").Single() == "test-key", "Authentication header");
    var payload = (await request.Content!.ReadFromJsonAsync<PalletScanBatchRequest>(ct))!;
    Check(payload.Items.Select(x => x.ScanId).SequenceEqual(original), "Retry payload");
    session.Add("NEW_DURING_UPLOAD", time);
    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(await service.UploadAsync(payload, ct)) };
};
Check(await session.UploadAsync(api) == 2 && session.Pending.Count == 1 && session.Pending[0].Sn == "NEW_DURING_UPLOAD", "Only acknowledged snapshot removed");
try { session.Add("000123", time); throw new Exception("Uploaded SN accepted again"); } catch (ArgumentException) { }
handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new PalletScanBatchResult(new[] { Guid.NewGuid() })) });
try { await session.UploadAsync(api); throw new Exception("Bad acknowledgement expected"); } catch (InvalidOperationException) { }
Check(session.Pending.Count == 1, "Mismatched acknowledgement retains records");

var large = new PalletScanSession();
for (int i = 0; i < 1001; i++) large.Add("SN" + i, time);
int batchCalls = 0;
handler.Respond = async (request, ct) =>
{
    var payload = (await request.Content!.ReadFromJsonAsync<PalletScanBatchRequest>(ct))!;
    batchCalls++;
    if (batchCalls == 2) return new HttpResponseMessage(HttpStatusCode.BadGateway);
    Check(payload.Items.Count == 1000, "Batch limit");
    return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new PalletScanBatchResult(payload.Items.Select(x => x.ScanId).ToArray())) };
};
try { await large.UploadAsync(api); throw new Exception("Second batch failure expected"); } catch (InvalidOperationException) { }
Check(large.Pending.Count == 1 && !large.IsUploading, "Retain failed batch only");
// Lookup validates before storage access and does not modify scan data.
repository.LookupResult = new PalletScanStoredRow("000123", 100, time.Date, time.DateTime);
Check((await service.FindBySnAsync(" 000123 "))?.PalletNumber == 100 && repository.LookupSn == "000123", "Lookup trim and leading zeros");
foreach (var badSn in new[] { "", " ", new string('X', 101) }) await Reject(() => service.FindBySnAsync(badSn));
repository.LookupResult = null;
Check(await service.FindBySnAsync("MISSING") is null, "Lookup missing result");
handler.Respond = (request, _) =>
{
    Check(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/api/tester-pallet-scans/lookup", "Lookup GET route");
    Check(request.RequestUri!.Query == "?sn=SN%2F%2B%23%3F%26", "Lookup encodes SN");
    Check(request.Headers.GetValues("X-Upload-Key").Single() == "test-key", "Lookup key");
    return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new PalletScanStoredRow("SN/+#?&", 1, time.Date, time.DateTime)) });
};
Check((await api.LookupAsync(" SN/+#?& ", "test-key"))?.PalletNumber == 1, "Lookup response and first pallet");
handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
Check(await api.LookupAsync("MISSING", "test-key") is null, "Lookup 404");
foreach (var status in new[] { HttpStatusCode.Unauthorized, HttpStatusCode.ServiceUnavailable })
{
    handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(status));
    try { await api.LookupAsync("000123", "test-key"); throw new Exception("Expected lookup failure"); }
    catch (InvalidOperationException) { }
}
handler.Respond = (_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new PalletScanStoredRow("OTHER-SN", 100, time.Date, time.DateTime)) });
try { await api.LookupAsync("000123", "test-key"); throw new Exception("Expected response mismatch failure"); }
catch (InvalidOperationException) { }
await Reject(() => api.LookupAsync(" ", "test-key"));
Console.WriteLine("PASS: pallet lookup validation, GET/key/encoded SN, found/missing/auth/server error and mismatched response.");
Console.WriteLine("PASS: validation, local scan date/time, pallet snapshot, duplicate SN, HTTP route/key/JSON, failure retention, retry IDs, acknowledgements, scans during upload and partial batch success.");

sealed class FakeRepository : IPalletScanRepository
{
    public string? LookupSn;
    public PalletScanStoredRow? LookupResult;
    public Task<PalletScanStoredRow?> FindBySnAsync(string sn, CancellationToken cancellationToken = default)
    {
        LookupSn = sn;
        return Task.FromResult(LookupResult);
    }
    public Task<IReadOnlyList<PalletScanStoredRow>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PalletScanStoredRow>>(Array.Empty<PalletScanStoredRow>());
    public int Calls;
    public IReadOnlyList<PalletScanItem>? Last;
    public Task SaveAsync(IReadOnlyList<PalletScanItem> items, CancellationToken cancellationToken = default) { Calls++; Last = items; return Task.CompletedTask; }
}
sealed class FakeHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> Respond { get; set; } = null!;
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => Respond(request, cancellationToken);
}
