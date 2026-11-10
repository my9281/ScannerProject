using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scanner.BreezaryStar.AndroidScanner.Services;
using Scanner.Server.Model;
using Scanner.Web.Controllers;
using Scanner.Web.Extensions;
using Scanner.Web.Models;
using Scanner.Web.Services;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
await MySqlGuidRegression.RunAsync(Check);
var root = Path.Combine(Path.GetTempPath(), "breezary-inbound-tests-" + Guid.NewGuid().ToString("N"));
var cache = new InboundCache(Path.Combine(root, "cache"));
var first = await cache.AddAsync(1, "  RAW / <code>  \r\n");
var duplicate = await cache.AddAsync(100, "  RAW / <code>  \r\n");
Check(first.Item.ScanId != duplicate.Item.ScanId, "Repeated barcode scans get distinct IDs");
Check(first.Item.Barcode == "  RAW / <code>  ", "Keep raw contents except scanner terminators");
cache = new InboundCache(Path.Combine(root, "cache"));
var restored = await cache.SnapshotAsync();
Check(restored.Count == 2 && restored.All(x => x.Item.DeviceId == first.Item.DeviceId), "Restart restores every pending scan and same device identity");
foreach (var invalid in new[] { 0, 101 })
{
    try { await cache.AddAsync(invalid, "bad"); throw new Exception("Invalid cache location accepted"); }
    catch (ArgumentException) { checks++; }
}
Check((await cache.SnapshotAsync()).Count == 2, "Invalid scan never changes cache");
using var simulated = new UploadServer();
using var simulatedHttp = new HttpClient(simulated);
var uploader = new InboundUploadClient(simulatedHttp);
simulated.LoseNextResponse = true;
try { await uploader.UploadPendingAsync(cache, "https://scanner.example/", "test-key"); throw new Exception("Lost response ignored"); }
catch (HttpRequestException) { checks++; }
Check((await cache.SnapshotAsync()).All(x => x.UploadedAt is null) && simulated.Items.Count == 2, "Unconfirmed successful remote upload remains pending locally");
Check(await uploader.UploadPendingAsync(cache, "https://scanner.example/", "test-key") == 2 && simulated.Items.Count == 2, "Retry is idempotent by scan ID");
Check((await new InboundCache(Path.Combine(root, "cache")).SnapshotAsync()).All(x => x.UploadedAt is not null), "Upload acknowledgements survive restart; history retained");
int calls = simulated.Calls;
Check(await uploader.UploadPendingAsync(cache, "https://scanner.example/", "test-key") == 0 && simulated.Calls == calls, "Already uploaded scans are not sent again");
var bulk = new InboundCache(Path.Combine(root, "bulk"));
for (int i = 0; i < 201; i++) await bulk.AddAsync(i % 100 + 1, "CODE-" + i);
simulated.FailAt = simulated.Calls + 2;
try { await uploader.UploadPendingAsync(bulk, "https://scanner.example/", "test-key"); throw new Exception("Partial failure ignored"); }
catch (InvalidOperationException) { checks++; }
Check((await bulk.SnapshotAsync()).Count(x => x.UploadedAt is null) == 1, "Partial upload confirms only completed chunks");
simulated.FailAt = 0;
Check(await uploader.UploadPendingAsync(bulk, "https://scanner.example/", "test-key") == 1, "Retry sends only the remaining chunk");
var badAckCache = new InboundCache(Path.Combine(root, "bad-ack"));
await badAckCache.AddAsync(3, "retain-me"); simulated.BadAck = true;
try { await uploader.UploadPendingAsync(badAckCache, "https://scanner.example/", "test-key"); throw new Exception("Invalid ack accepted"); }
catch (InvalidOperationException) { checks++; }
Check((await badAckCache.SnapshotAsync()).Single().UploadedAt is null, "Unknown acknowledgement never clears a pending scan");
foreach (var value in new[] { "http://scanner.example/", "https://name:secret@scanner.example/", "https://scanner.example/?key=x" })
{
    try { InboundUploadClient.Server(value); throw new Exception("Unsafe server accepted"); }
    catch (ArgumentException) { checks++; }
}

var store = new MemoryStore();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = Environments.Development,
    ContentRootPath = root, WebRootPath = Path.GetFullPath("Scanner.Web/wwwroot") });
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["Upload:ApiKey"] = "test-key" });
builder.Services.AddScannerWeb(builder.Configuration);
builder.Services.AddControllers().AddApplicationPart(typeof(BreezaryInboundScansController).Assembly);
builder.Services.AddSingleton<IBreezaryInboundStore>(store);
builder.Services.AddSingleton<IAccountService, Accounts>();
await using var app = builder.Build(); app.UseStaticFiles(); app.MapControllers(); await app.StartAsync();
using var http = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
async Task<HttpResponseMessage> Send(HttpMethod method, object? body = null, string? apiKey = null, string? role = null, string query = "")
{
    using var request = new HttpRequestMessage(method, "/api/breezary-inbound-scans" + query);
    if (body is not null) request.Content = JsonContent.Create(body);
    if (apiKey is not null) request.Headers.Add("X-Upload-Key", apiKey);
    if (role is not null) request.Headers.Authorization = new("Bearer", role);
    return await http.SendAsync(request);
}
try
{
    var batch = new BreezaryInboundBatch([first.Item, duplicate.Item]);
    foreach (var key in new string?[] { null, "wrong" })
    {
        using var denied = await Send(HttpMethod.Post, batch, key);
        Check(denied.StatusCode == HttpStatusCode.Unauthorized, "Upload requires valid device API key");
    }
    Check(store.Count == 0, "Unauthorized uploads never write records");
    using (var accepted = await Send(HttpMethod.Post, batch, "test-key"))
    {
        var result = await accepted.Content.ReadFromJsonAsync<BreezaryInboundAcknowledgement>();
        Check(accepted.IsSuccessStatusCode && result!.AcknowledgedIds.Count == 2 && store.Count == 2, "Upload confirms committed scan identities");
    }
    using (var retry = await Send(HttpMethod.Post, batch, "test-key"))
        Check(retry.IsSuccessStatusCode && store.Count == 2, "Repeated request does not duplicate scans");
    using (var conflict = await Send(HttpMethod.Post, new BreezaryInboundBatch([first.Item with { Barcode = "different" }]), "test-key"))
        Check(conflict.StatusCode == HttpStatusCode.Conflict && store.Count == 2, "Reusing scan ID with changed content is rejected");
    foreach (var item in new[] { first.Item with { CacheLocation = 101 }, first.Item with { Barcode = "" }, first.Item with { ScanId = Guid.Empty } })
    {
        using var invalid = await Send(HttpMethod.Post, new BreezaryInboundBatch([item]), "test-key");
        Check(invalid.StatusCode == HttpStatusCode.BadRequest, "Validate inbound item before persistence");
    }
    foreach (var role in new string?[] { null, "user", "viewer" })
    {
        using var denied = await Send(HttpMethod.Get, role: role, apiKey: "test-key");
        Check(denied.StatusCode == (role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden), "Device key does not grant Web read access");
    }
    using (var page = await Send(HttpMethod.Get, role: "admin", query: "?limit=1"))
    {
        var result = await page.Content.ReadFromJsonAsync<BreezaryInboundPage>();
        Check(page.IsSuccessStatusCode && result!.HasMore && result.Records.Count == 1, "Web list paginates uploaded records");
        using var next = await Send(HttpMethod.Get, role: "admin", query: "?beforeId=" + result!.NextId);
        var remainder = await next.Content.ReadFromJsonAsync<BreezaryInboundPage>();
        Check(remainder!.Records.Count == 1 && remainder.Records[0].ScanId != result.Records[0].ScanId, "Cursor avoids duplicate rows");
    }
    using (var filtered = await Send(HttpMethod.Get, role: "admin", query: "?location=100&search=RAW"))
        Check((await filtered.Content.ReadFromJsonAsync<BreezaryInboundPage>())!.Records.Single().CacheLocation == 100, "Web filters cache location and raw content");
    using (var bad = await Send(HttpMethod.Get, role: "admin", query: "?location=101"))
        Check(bad.StatusCode == HttpStatusCode.BadRequest, "Reject invalid list filters");
    Console.WriteLine($"Passed {checks} inbound cache/upload/API checks; no production database or server accessed.");
    if (args.Contains("--preview")) { Console.WriteLine("Preview: " + http.BaseAddress + "breezary-inbound.html (fake login admin / preview)"); await app.WaitForShutdownAsync(); }
}
finally { await app.StopAsync(); }

sealed class UploadServer : HttpMessageHandler
{
    public readonly Dictionary<Guid, BreezaryInboundItem> Items = new();
    public int Calls, FailAt;
    public bool LoseNextResponse, BadAck;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        if (request.RequestUri!.ToString() != "https://scanner.example/api/breezary-inbound-scans" || request.Headers.GetValues("X-Upload-Key").Single() != "test-key")
            throw new Exception("Incorrect upload destination or authentication");
        if (Calls == FailAt) return new(HttpStatusCode.ServiceUnavailable) { Content = JsonContent.Create(new { message = "simulated server unavailable" }) };
        var batch = await request.Content!.ReadFromJsonAsync<BreezaryInboundBatch>(ct);
        BreezaryInboundValidation.Validate(batch!.Items);
        foreach (var item in batch.Items) Items.TryAdd(item.ScanId, item);
        if (LoseNextResponse) { LoseNextResponse = false; throw new HttpRequestException("simulated lost response after server commit"); }
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new BreezaryInboundAcknowledgement(
            BadAck ? [Guid.NewGuid()] : batch.Items.Select(x => x.ScanId).ToArray(), DateTimeOffset.UtcNow)) };
    }
}
sealed class MemoryStore : IBreezaryInboundStore
{
    private readonly List<BreezaryInboundStored> rows = [];
    public int Count => rows.Count;
    public Task SaveAsync(IReadOnlyList<BreezaryInboundItem> items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            var old = rows.Find(x => x.ScanId == item.ScanId);
            if (old is not null && (old.DeviceId != item.DeviceId || old.Barcode != item.Barcode || old.CacheLocation != item.CacheLocation || old.ScannedAt != item.ScannedAt)) throw new ApiException(409, "immutable scan conflict");
        }
        foreach (var item in items)
            if (!rows.Any(x => x.ScanId == item.ScanId)) rows.Add(new((ulong)rows.Count + 1, item.ScanId, item.DeviceId, item.CacheLocation, item.Barcode, item.ScannedAt, DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }
    public Task<BreezaryInboundPage> ListAsync(ulong before, int limit, int? location, string? search, CancellationToken ct)
    {
        var selected = rows.Where(x => (before == 0 || x.Id < before) && (location is null || x.CacheLocation == location)
            && (string.IsNullOrEmpty(search) || x.Barcode.Contains(search))).OrderByDescending(x => x.Id).Take(limit + 1).ToList();
        bool more = selected.Count > limit;
        if (more) selected.RemoveAt(selected.Count - 1);
        return Task.FromResult(new BreezaryInboundPage(selected, more, more ? selected[^1].Id : 0));
    }
}
sealed class Accounts : IAccountService
{
    public Task<AccountResult?> GetCurrentAsync(string token, CancellationToken ct = default) => Task.FromResult<AccountResult?>(
        token is "admin" or "user" or "viewer" ? new("test", null, new AccountDomain(1, "test"), token, token, DateTimeOffset.UtcNow.AddHours(1)) : null);
    public Task<AccountResult?> LoginAsync(LoginRequest request, CancellationToken ct = default) => request.Password == "preview" ? GetCurrentAsync(request.Username, ct) : Task.FromResult<AccountResult?>(null);
    public Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<AccountDomain>> GetDomainsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task LogoutAsync(string token, CancellationToken ct = default) => Task.CompletedTask;
}
