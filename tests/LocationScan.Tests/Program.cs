using System.Net;
using System.Net.Http.Json;
using Scanner.Server.Model;
using Scanner.Server.BLL;
using Scanner.AndroidTester.Services;
var repo = new FakeRepository();
var service = new LocationService(repo);
var time = DateTimeOffset.Now;
var result = await service.UploadAsync(new(new[] { new LocationScanItem(" 001 ", time), new LocationScanItem("001", time), new LocationScanItem("a", time), new LocationScanItem("A", time) }));
Check(repo.Last!.Count == 3 && result.AcceptedLocationIds.SequenceEqual(new[] { "001", "a", "A" }), "server trim/dedup/case/leading zero");
foreach (var id in new[] { " ", new string('x', 101), "A\nB" })
{ try { await service.UploadAsync(new(new[] { new LocationScanItem(id, time) })); throw new Exception("Invalid accepted"); } catch (ArgumentException) { } }
var session = new LocationScanSession { ApiKey = "key" }; session.Add(" 001 ", time);
try { session.Add("001", time); throw new Exception("Duplicate accepted"); } catch (ArgumentException) { }
var handler = new Handler(); var api = new LocationScanApiService(new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") });
try { await session.UploadAsync(api); throw new Exception("Failure expected"); } catch (InvalidOperationException) { }
Check(session.Pending.Count == 1 && !session.IsUploading, "failure retained");
handler.Success = true;
Check(await session.UploadAsync(api) == 1 && session.Pending.Count == 0, "ack removes pending");
try { session.Add("001", time); throw new Exception("Uploaded duplicate accepted"); } catch (ArgumentException) { }
session.Add("002", time); handler.BadAck = true;
try { await session.UploadAsync(api); throw new Exception("Bad ack accepted"); } catch (InvalidOperationException) { }
Check(session.Pending.Count == 1, "bad acknowledgement retained");
Console.WriteLine("PASS: server dedup/validation, PDA dedup, retries, route/key and acknowledgements.");
static void Check(bool ok, string name) { if (!ok) throw new Exception(name); }
sealed class Handler : HttpMessageHandler
{
    public bool Success, BadAck;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.RequestUri!.AbsolutePath != "/api/warehouse-locations" || request.Headers.GetValues("X-Upload-Key").Single() != "key") throw new Exception("route/key");
        if (!Success) return new(HttpStatusCode.ServiceUnavailable);
        var body = (await request.Content!.ReadFromJsonAsync<LocationBatchRequest>(ct))!;
        return new(HttpStatusCode.OK) { Content = JsonContent.Create(new LocationBatchResult(BadAck ? new[] { "wrong" } : body.Items.Select(x => x.LocationId).ToArray())) };
    }
}
sealed class FakeRepository : Scanner.Server.DAL.ILocationRepository
{
    public IReadOnlyList<LocationScanItem>? Last;
    public Task SaveAsync(IReadOnlyList<LocationScanItem> items, CancellationToken ct = default) { Last = items; return Task.CompletedTask; }
    public Task<IReadOnlyList<LocationRow>> GetAllAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<LocationRow>>(Array.Empty<LocationRow>());
}

