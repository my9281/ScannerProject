using Scanner.Helpers.Services;
using Scanner.Models;
using System.Net;
using Newtonsoft.Json;
var id = Guid.NewGuid();
var handler = new BatchHandler(id);
var api = new ShelvedPalletApiService(new HttpClient(handler) { BaseAddress = new Uri("https://example.invalid/") });
var list = await api.GetSkuSnBatchesAsync("test");
if (list.Count != 1 || list[0].BatchId != id) throw new Exception("Batch list");
var batch = await api.GetSkuSnBatchAsync(id, "test");
var records = OutboundInspectionService.BuildLines(batch.Items.SelectMany(x => new[] { x.Sku, x.Sn }).ToArray(), new[] { new InboundChecklistRecord { Sn = "000123", Type = "test", DetectionStatus = "良品" } });
if (records.Count != 1 || records[0].Sku != "SKU-001" || records[0].Sn != "000123" || !records[0].IsMatched) throw new Exception("Outbound mapping");
Console.WriteLine("PASS: authenticated batch list/detail, SKU/SN download and outbound matching.");
sealed class BatchHandler(Guid id) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Headers.GetValues("X-Upload-Key").Single() != "test") throw new Exception("Key");
        object body = request.RequestUri!.AbsolutePath switch {
            "/api/sku-sn-batches" => new[] { new { BatchId = id, BatchNumber = "202610091234", ItemCount = 1 } },
            var path when path == "/api/sku-sn-batches/" + id.ToString("D") => new { BatchId = id, BatchNumber = "202610091234", Items = new[] { new { Sku = "SKU-001", Sn = "000123" } } },
            _ => throw new Exception("Route")
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(JsonConvert.SerializeObject(body)) });
    }
}
