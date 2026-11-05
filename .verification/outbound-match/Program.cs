using Scanner.WPF.Services;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Net.Http;
using System.Text;
using System.Collections.Concurrent;
using System.IO;

string root=Path.GetFullPath(args.Length>0?args[0]:".verification/outbound-match");
string cacheRoot=Path.GetFullPath(".verification/outbound-match");
var inputs=JArray.Parse(await File.ReadAllTextAsync(Path.Combine(root,"input.json")));
var targets=inputs.Select(r=>((string)r[2]??"").Trim()).Where(s=>s.Length>0).ToHashSet(StringComparer.OrdinalIgnoreCase);
var config=new BluettiConfigStore().Load()??throw new Exception("No saved login config");
var sessions=new BluettiSessionService();
Console.WriteLine("Refreshing saved login");
var session=await sessions.LoginAsync(config.Username,config.Password,CancellationToken.None);
Console.WriteLine("Login succeeded; checking site");
var sites=await sessions.GetSitesAsync(session,CancellationToken.None);
var site=sites.SingleOrDefault(s=>s.SiteName=="美国Logistar售后服务站（委外）")??throw new Exception("Configured Logistar site unavailable");
await sessions.SelectSiteAsync(session,site,CancellationToken.None);
sessions.Save(session);
Console.WriteLine("Site: "+session.SiteName);
using var client=new HttpClient(new HttpClientHandler {AllowAutoRedirect=false,UseCookies=false}) {Timeout=TimeSpan.FromSeconds(60)};
string Text(JToken t,string k)=>((string)t?[k]??"").Trim();
async Task<JToken> Send(string path,JObject payload=null)
{
 for(int attempt=0;;attempt++)try
 {
  using var request=new HttpRequestMessage(payload==null?HttpMethod.Get:HttpMethod.Post,"https://gwpry.bluettipower.com/api/bluas/client/outOperation/"+path);
  request.Headers.Add("Authorization",session.Token);request.Headers.Add("x-app-resource",session.SiteId);
  request.Headers.Add("x-app-key","18880A4BA6CF791157564C2ECD0");request.Headers.Add("x-os","web");request.Headers.Add("Accept-Language","zh-CN");
  request.Headers.Add("Origin","https://after-sales.bluettipower.com");request.Headers.Referrer=new Uri("https://after-sales.bluettipower.com/");
  if(payload!=null)request.Content=new StringContent(payload.ToString(Formatting.None),Encoding.UTF8,"application/json");
  using var response=await client.SendAsync(request);
  response.EnsureSuccessStatusCode();
  var envelope=JObject.Parse(await response.Content.ReadAsStringAsync());
  if((int?)envelope["msgCode"]!=0)throw new InvalidOperationException("API code "+envelope["msgCode"]);
  return envelope["data"];
 }
 catch(Exception ex)when(attempt<3&&(ex is HttpRequestException||ex is TaskCanceledException)){await Task.Delay((attempt+1)*1500);}
}
JObject Product(JToken p)=>new JObject {["sn"]=Text(p,"sn"),["refSn"]=Text(p,"refSn"),["sku"]=Text(p,"sku"),["count"]=p["count"]};
var categories=new Dictionary<int,string>{{1,"出库工单需求池"},{2,"其他需求工单池"},{3,"二手品销售出库"},{4,"以旧换新退货"},{5,"其他售后站点工单出库"}};
var all=new List<JObject>();var totals=new JObject();
foreach(var(type,name)in categories)
{
 int expected=-1,seen=0;
 for(int page=1;;page++)
 {
  string cache=Path.Combine(cacheRoot,$"list-{type}-{page}.json");JObject clean;
  if(File.Exists(cache))clean=JObject.Parse(await File.ReadAllTextAsync(cache));
  else
  {
   var payload=BluettiLabelApi.BuildPayload(session.SiteId,"",page);payload["type"]=type;payload["pageSize"]=100;payload["logisticsCompany"]="";payload["shippingOrderNo"]="";
   var data=await Send("getShippingList",payload);
   if(data?["content"]is not JArray content||!int.TryParse((string)data["totalElements"],out int total)||total<0)throw new Exception("Invalid pagination");
   clean=new JObject {["total"]=total,["rows"]=new JArray(content.Select(o=>new JObject {
    ["category"]=type,["workOrderId"]=Text(o,"workOrderId"),["outOperationId"]=Text(o,"outOperationId"),["rmaNo"]=Text(o,"rmaNo"),["listOutTime"]=Text(o,"outTime"),
    ["products"]=new JArray((o["productMaterialVO"]as JArray??new JArray()).Select(Product))}))};
   await File.WriteAllTextAsync(cache,clean.ToString(Formatting.None));await Task.Delay(150);
  }
  int totalCount=(int)clean["total"];if(expected<0)expected=totalCount;else if(expected!=totalCount)throw new Exception("List changed during pagination");
  var rows=(JArray)clean["rows"];all.AddRange(rows.Cast<JObject>());seen+=rows.Count;
  Console.WriteLine($"LIST type={type} page={page} seen={seen}/{expected}");
  if(seen>=expected){if(seen!=expected)throw new Exception("Pagination overflow");break;}
  if(rows.Count==0||page>10000)throw new Exception("Incomplete pagination");
 }
 totals[name]=expected;
}
Console.WriteLine("LIST COMPLETE orders="+all.Count+" missingProducts="+all.Count(o=>!o["products"].Any()));
var candidates=all.Where(o=>o["products"].Any(p=>targets.Contains(Text(p,"sn")))).GroupBy(o=>o["category"]+":"+Text(o,"workOrderId")).Select(g=>g.First()).ToList();
Console.WriteLine("DETAIL candidates="+candidates.Count);
var matches=new ConcurrentBag<JObject>();var errors=new ConcurrentBag<JObject>();int done=0;
await Parallel.ForEachAsync(candidates,new ParallelOptions {MaxDegreeOfParallelism=3},async(order,ct)=>
{
 string id=Text(order,"workOrderId");int type=(int)order["category"];
 try
 {
  if(id.Length==0)throw new Exception("Missing workOrderId");
  string cache=Path.Combine(cacheRoot,$"detail-{type}-{id}.json");JObject clean;
  if(File.Exists(cache))clean=JObject.Parse(await File.ReadAllTextAsync(cache));
  else
  {
   var data=await Send("shipmentDetail?workOrderId="+Uri.EscapeDataString(id)+"&orderType="+type);
   if(data?["shipmentInfoList"]is not JArray shipments)throw new Exception("Missing shipmentInfoList");
   clean=new JObject {["shipments"]=new JArray(shipments.Select(s=>new JObject {
    ["shipmentNo"]=Text(s,"shipmentNo"),["outTime"]=Text(s,"outTime"),["handlerTime"]=Text(s,"handlerTime"),
    ["products"]=new JArray((s["productMaterialInfos"]as JArray??new JArray()).Select(Product))}))};
   await File.WriteAllTextAsync(cache,clean.ToString(Formatting.None));await Task.Delay(180,ct);
  }
  var exact=new List<JObject>();
  foreach(var ship in clean["shipments"])
   foreach(var p in ship["products"].Where(p=>targets.Contains(Text(p,"sn"))))
    exact.Add(new JObject {["sn"]=Text(p,"sn"),["sku"]=Text(p,"sku"),["count"]=p["count"],["outTime"]=Text(ship,"outTime"),["handlerTime"]=Text(ship,"handlerTime"),
     ["shipmentNo"]=Text(ship,"shipmentNo"),["workOrderId"]=id,["category"]=categories[type],["orderType"]=type,["rmaNo"]=Text(order,"rmaNo"),
     ["source"]="https://after-sales.bluettipower.com/index.html#/afterSaleClient/inventory/client/outBoundRefactor/route/v2/view?id="+Uri.EscapeDataString(id)+"&orderType="+type+"&active=1"});
  foreach(var hit in exact)matches.Add(hit);
  foreach(var sn in order["products"].Select(p=>Text(p,"sn")).Where(targets.Contains).Distinct(StringComparer.OrdinalIgnoreCase))
   if(!exact.Any(m=>string.Equals(Text(m,"sn"),sn,StringComparison.OrdinalIgnoreCase)))errors.Add(new JObject {["sn"]=sn,["reason"]="列表有该SN，详情未匹配该SN",["workOrderId"]=id});
 }
 catch(Exception ex){foreach(var sn in order["products"].Select(p=>Text(p,"sn")).Where(targets.Contains).Distinct())errors.Add(new JObject {["sn"]=sn,["reason"]=ex.GetType().Name+": "+ex.Message,["workOrderId"]=id});}
 int n=Interlocked.Increment(ref done);if(n%20==0||n==candidates.Count)Console.WriteLine($"DETAIL {n}/{candidates.Count} matches={matches.Count} errors={errors.Count}");
});
var unique=new JArray(matches.GroupBy(m=>Text(m,"sn").ToUpperInvariant()+"|"+Text(m,"shipmentNo")+"|"+Text(m,"workOrderId")+"|"+Text(m,"outTime")).Select(g=>g.First()).OrderBy(m=>Text(m,"sn")).ThenBy(m=>Text(m,"outTime")));
var result=new JObject {["siteName"]=session.SiteName,["queryTimeUtc"]=DateTimeOffset.UtcNow.ToString("o"),["totals"]=totals,["listRows"]=all.Count,["missingProductRows"]=all.Count(o=>!o["products"].Any()),["candidateOrders"]=candidates.Count,["matches"]=unique,["errors"]=new JArray(errors)};
await File.WriteAllTextAsync(Path.Combine(root,"result.json"),result.ToString(Formatting.None));
Console.WriteLine("COMPLETE matched_unique_SN="+unique.Select(m=>Text(m,"sn").ToUpperInvariant()).Distinct().Count()+" records="+unique.Count+" errors="+errors.Count);
