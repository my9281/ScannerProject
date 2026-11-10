using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Scanner.Helpers.Services;
using Scanner.Models;
using Scanner.Server.BLL;
using Scanner.Server.DAL;
using Scanner.Server.Model;

void Check(bool value, string message) { if (!value) throw new Exception(message); }
var handler = new RetryHandler();
string server = "https://" + Guid.NewGuid().ToString("N") + ".invalid/";
var source = new[] { new OutboundInspectionRecord { Number=1, Sn="AbC001", Sku="Sku1" } };
var api = new ShelvedPalletApiService(new HttpClient(handler) { BaseAddress=new Uri(server) });
try { await api.UploadAsync("P1", source); throw new Exception("Failure required"); } catch (InvalidOperationException) { }
var first = handler.Ids.Single();
// 新客户端实例模拟重启：磁盘日志仍必须复用原请求。
api = new ShelvedPalletApiService(new HttpClient(handler) { BaseAddress=new Uri(server) });
handler.Fail=false;
var response = await api.UploadAsync("P1", source);
Check(handler.Ids[1]==first && response.RequestId==first, "Restart retry retains request identity");
await api.UploadAsync("P1", source);
Check(handler.Ids[2]!=first, "Confirmed new operation gets new identity");
handler.BadReceipt=true;
try { await api.UploadAsync("P1", source); throw new Exception("Bad receipt accepted"); } catch (InvalidOperationException) { }
var uncertain=handler.Ids.Last(); handler.BadReceipt=false;
await api.UploadAsync("P1", source);
Check(handler.Ids.Last()==uncertain, "Bad acknowledgement retains original identity");
var repo = new MemoryRepository();
var service=new ShelvedPalletService(repo);
var id=Guid.NewGuid();
var request=new CreateShelvedPalletBatchRequest(" P1 ",new[]{new ShelvedPalletItemRequest(1," AbC001 "," Sku1 ",null,null,null)},id);
var result=await service.CreateBatchAsync(request);
Check(repo.LastId==id && repo.LastSn=="AbC001" && result.RequestId==id, "Server normalizes edges and preserves SN case and request ID");
try { await service.CreateBatchAsync(request with {RequestId=Guid.Empty}); throw new Exception("Empty ID accepted"); } catch(ArgumentException) { }
var batch = new SkuSnBatch(Guid.NewGuid(),"202610091234",DateTimeOffset.Now,new[]{new SkuSnItem("S","AbC"),new SkuSnItem("S","abc")});
try { SkuSnBatchValidator.Validate(batch); throw new Exception("Case-insensitive duplicate accepted"); } catch (ArgumentException) { }
Check(Attribute.IsDefined(typeof(Scanner.Web.Controllers.LocationStatusController).GetMethod("SetStatus")!,typeof(Scanner.Web.Filters.AccountPermissionAttribute)), "Location management requires account permission");
Console.WriteLine("PASS: durable restart retry identity, new operations, bad acknowledgements, case preservation and server request forwarding (no production DB).");

sealed class RetryHandler : HttpMessageHandler
{
 public bool Fail=true, BadReceipt;
 public List<Guid> Ids=new();
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken ct)
 {
  var payload=JObject.Parse(await r.Content!.ReadAsStringAsync(ct));var id=Guid.Parse((string)payload["RequestId"]!);Ids.Add(id);
  if(Fail)return new(HttpStatusCode.BadGateway);
  return new(HttpStatusCode.OK){Content=new StringContent(JsonConvert.SerializeObject(new {requestId=BadReceipt?Guid.NewGuid():id,palletNumber="P1",insertedCount=1,uuids=new[]{"row"}}))};
 }
}
sealed class MemoryRepository : IShelvedPalletRepository
{
 public Guid? LastId; public string? LastSn;
 public Task<IReadOnlyList<ShelvedPalletRecord>> CreateBatchAsync(string pallet,DateTime time,IReadOnlyList<ShelvedPalletItemRequest> items,CancellationToken ct=default,Guid? requestId=null)
 {LastId=requestId;LastSn=items[0].Sn;return Task.FromResult<IReadOnlyList<ShelvedPalletRecord>>(new[]{new ShelvedPalletRecord("uuid",1,items[0].Sn,items[0].Sku,null,null,null,new(2026,10,9),pallet,new(10,0))});}
 public Task<IReadOnlyList<ShelvedPalletRecord>> QueryAsync(DateTime? f,DateTime? t,string? p,int l,CancellationToken ct=default)=>throw new NotSupportedException();
 public Task<ShelvedPalletTableResult> GetAllAsync(CancellationToken ct=default)=>throw new NotSupportedException();
}
