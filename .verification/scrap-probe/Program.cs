using Scanner.WPF.Services;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using System.IO;
var session=new BluettiSessionService().Load()??throw new Exception("Saved session missing");
using var http=new HttpClient {Timeout=TimeSpan.FromSeconds(30)};
async Task<JObject> Read(string path,JObject payload=null)
{
 using var request=new HttpRequestMessage(payload==null?HttpMethod.Get:HttpMethod.Post,"https://gwpry.bluettipower.com/api/bluas/client/"+path);
 request.Headers.Add("Authorization",session.Token);request.Headers.Add("x-app-resource",session.SiteId);request.Headers.Add("x-app-key","18880A4BA6CF791157564C2ECD0");request.Headers.Add("x-os","web");request.Headers.Add("Accept-Language","zh-CN");
 if(payload!=null)request.Content=new StringContent(payload.ToString(),Encoding.UTF8,"application/json");
 using var response=await http.SendAsync(request);response.EnsureSuccessStatusCode();return JObject.Parse(await response.Content.ReadAsStringAsync());
}
var account=await Read("user/v1/getUserName");
Console.WriteLine(new JObject {["msgCode"]=account["msgCode"],["timezoneName"]=account["data"]?["timezoneName"],["nationalTimezone"]=account["data"]?["nationalTimezone"]});
var payload=new JObject {["warehouseMasterType"]=104,["sn"]="AC602316001060676",["pageNo"]=1,["pageSize"]=20};
foreach(var k in new[]{"number","outInType","sku","nodeType","category","shippingOrderNo","handlerDate","warehouseType","rmaNo","startHandlerDate","endHandlerDate"})payload[k]="";
var sample=await Read("warehouse/segmentation/page",payload);
Console.WriteLine("code="+sample["code"]+" msgCode="+sample["msgCode"]);
foreach(var r in sample["data"]?["content"]??new JArray())Console.WriteLine(new JObject {["sn"]=r["sn"],["handlerDate"]=r["handlerDate"],["businessTime"]=r["businessTime"],["number"]=r["number"],["direction"]=r["i18nOutIn"],["nodeType"]=r["i18nNodeType"]});
if(args.Contains("--all")||args.Contains("--inventory"))
{
 bool inventory=args.Contains("--inventory");
 var rows=new JArray();int expected=-1;
 payload["sn"]="";payload["pageSize"]=100;
 if(inventory){payload["warehouseMasterType"]="";payload["outInType"]=1;payload["pageSize"]=500;}
 for(int page=1;;page++)
 {
  payload["pageNo"]=page;var res=await Read("warehouse/segmentation/page",payload);
  if((int?)res["code"]!=0||res["data"]?["content"]is not JArray content)throw new Exception("Invalid inventory page");
  int total=int.Parse((string)res["data"]["totalElements"]);if(expected<0)expected=total;else if(total!=expected)throw new Exception("Inventory changed during pagination");
  foreach(var r in content)rows.Add(new JObject {["sn"]=r["sn"],["handlerDate"]=r["handlerDate"],["businessTime"]=r["businessTime"],["number"]=r["number"],["direction"]=r["i18nOutIn"],["nodeType"]=r["i18nNodeType"],["sku"]=r["sku"],["quantity"]=r["quantity"],["warehouseType"]=r["warehouseType"],["warehouse"]=r["i18nWarehouseType"],["targetWarehouse"]=r["i18nTargetWarehouseType"]});
  Console.WriteLine($"PAGE {page}: {rows.Count}/{expected}");
  if(rows.Count>=expected){if(rows.Count!=expected)throw new Exception("Pagination overflow");break;}
  if(content.Count==0)throw new Exception("Incomplete pagination");
  await Task.Delay(120);
 }
 await File.WriteAllTextAsync(inventory?".verification/inventory-probe/page-times.json":".verification/scrap-probe/page-times.json",rows.ToString(Newtonsoft.Json.Formatting.None));
}
