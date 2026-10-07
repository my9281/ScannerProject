using Scanner.Helpers.Services;
using System.Net;

var handler = new FakeHandler();
using var client = new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") };
var service = new ShelvedPalletApiService(client);
var rows = await service.GetWarehouseInventoryAsync("test-key");
if (rows.Count != 1 || rows[0].Sn != "000123" || rows[0].Sku != "P-AC180" || rows[0].PalletNumber != "PALLET-1" || rows[0].ShelvingDate != new DateTime(2026, 9, 1)) throw new Exception("Web inventory schema mismatch");
handler.Status = HttpStatusCode.Unauthorized;
try { await service.GetWarehouseInventoryAsync("test-key"); throw new Exception("Unauthorized response accepted"); } catch (InvalidOperationException) { }
handler.Status = HttpStatusCode.OK;
handler.Body = "{\"count\":0,\"rows\":[]}";
if ((await service.GetWarehouseInventoryAsync("test-key")).Count != 0) throw new Exception("Empty inventory mismatch");
handler.Body = "{\"count\":1}";
try { await service.GetWarehouseInventoryAsync("test-key"); throw new Exception("Invalid response accepted"); } catch (InvalidOperationException) { }
Console.WriteLine("PASS: Web inventory endpoint, authentication header, table schema, leading-zero SN, shelving date, empty inventory and error responses; no live server accessed.");

sealed class FakeHandler : HttpMessageHandler
{
    public HttpStatusCode Status = HttpStatusCode.OK;
    public string Body = "{\"columns\":[\"sn\",\"sku\"],\"count\":1,\"rows\":[{\"sn\":\"000123\",\"sku\":\"P-AC180\",\"shelving_date\":\"2026-09-01T00:00:00\",\"pallet_number\":\"PALLET-1\"}]}";
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.Method != HttpMethod.Get || request.RequestUri!.AbsolutePath != "/api/shelved-pallets/all" || request.Headers.GetValues("X-Upload-Key").Single() != "test-key") throw new Exception("Request mismatch");
        return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
    }
}
