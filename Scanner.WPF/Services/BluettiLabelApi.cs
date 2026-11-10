using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Scanner.WPF.Services
{
    public interface IBluettiLabelLookup
    {
        Task<BluettiLabelRow> QueryAsync(BluettiSession session, string sn, CancellationToken token);
    }

    // Request contracts verified against the website's outboundWorkOrderPool/index.vue,
    // noodleList.vue and assets/utils/api.js (20261013_01).
    public sealed class BluettiLabelApi : IBluettiLabelLookup
    {
        public const string SearchPath = "/api/bluas/client/outOperation/salePage";
        public const string CustomerPath = "/api/bluas/client/outOperation/customerInfo";
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { Timeout = Timeout.InfiniteTimeSpan };
        private readonly HttpClient _client;
        public BluettiLabelApi(HttpClient client = null) { _client = client ?? Client; }

        public static JObject BuildPayload(string siteId, string sn, int page)
        {
            var result = new JObject();
            foreach (var key in new[] { "workOrderNo", "shipper", "status", "handler", "customerPhone", "customerName", "customerEmail", "customerAddress", "outWarehouseStatus", "model", "rmaNo", "sku" }) result[key] = "";
            result["siteId"] = siteId; result["sn"] = sn; result["pageNo"] = page; result["pageSize"] = 20;
            return result;
        }

        public async Task<BluettiLabelRow> QueryAsync(BluettiSession session, string sn, CancellationToken cancellationToken)
        {
            sn = ValidateSn(sn);
            if (string.IsNullOrWhiteSpace(session?.Token) || string.IsNullOrWhiteSpace(session.SiteId)) throw new BluettiAuthenticationException();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            deadline.CancelAfter(TimeSpan.FromSeconds(60));
            var token = deadline.Token;
            try
            {
                var matches = new Dictionary<string, (JObject Order, JObject Product)>();
                int seen = 0;
                for (int page = 1; ; page++)
                {
                    var data = await PostAsync(session, SearchPath, BuildPayload(session.SiteId, sn, page), token);
                    if (!(data?["content"] is JArray orders)) throw new InvalidDataException("工单 API 缺少查询结果列表。");
                    // The live service serializes totalElements as a decimal string; fixtures
                    // and other deployments may return a JSON integer. Reject fractional,
                    // negative, missing or overflowing totals rather than truncate them.
                    var totalValue = data["totalElements"];
                    if (totalValue == null || (totalValue.Type != JTokenType.Integer && totalValue.Type != JTokenType.String) ||
                        !int.TryParse(totalValue.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out int total))
                        throw new InvalidDataException("工单 API 返回的分页总数无效，未写入 Excel。");
                    if (total < 0 || orders.Count > 20 || total < seen + orders.Count) throw new InvalidDataException("工单 API 分页数据异常。");
                    foreach (var order in orders.OfType<JObject>())
                    {
                        if (!(order["productMaterialVO"] is JArray products)) throw new InvalidDataException("工单 API 缺少产品数据。");
                        var exact = products.OfType<JObject>().Where(p => string.Equals(Text(p, "sn"), sn, StringComparison.OrdinalIgnoreCase)).ToArray();
                        if (exact.Length == 0) continue;
                        if (exact.Select(p => Text(p, "sku")).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1) throw new InvalidDataException("同一 SN 对应多个 SKU，未写入 Excel。");
                        var id = Text(order, "workOrderId");
                        if (id.Length == 0 || order["type"]?.Type != JTokenType.Integer) throw new InvalidDataException("工单缺少编号或类型。");
                        matches[id + ":" + order["type"]] = (order, exact[0]);
                        if (matches.Count > 1) throw new InvalidDataException("同一 SN 匹配多个工单，未写入 Excel，请先核对。");
                    }
                    seen += orders.Count;
                    if (seen >= total) break;
                    if (orders.Count == 0 || page >= 50) throw new InvalidDataException("无法完整核对工单分页，未写入 Excel。");
                }
                if (matches.Count == 0) return null;
                var match = matches.Values.Single();
                string workOrder = Text(match.Order, "workOrderId");
                var customer = await PostAsync(session, CustomerPath + "?workOrderId=" + Uri.EscapeDataString(workOrder) + "&orderType=" + match.Order.Value<int>("type"), null, token);
                if (!(customer is JObject info)) throw new InvalidDataException("面单 API 返回格式已变化。");
                var sku = Text(match.Product, "sku");
                if (sku.Length == 0) sku = Text(match.Product, "model");
                var parts = sku.Split('-');
                var row = new BluettiLabelRow
                {
                    Sn = Text(match.Product, "sn"), Sku = sku,
                    Model = parts.Length > 2 && parts[0] == "P" ? parts[1] : Text(match.Product, "model"),
                    Rma = Text(match.Order, "rmaNo"), WorkOrder = workOrder,
                    Name = Text(info, "customerName"), Email = Text(info, "customerEmail"), Phone = Text(info, "customerPhone"),
                    Address = Text(info, "shippingAddress")
                };
                if (row.Address.Length == 0) row.Address = Text(info, "customerAddress");
                if (row.Rma.Length == 0 || row.Sku.Length == 0 || row.Address.Length == 0) throw new InvalidDataException("工单或面单数据不完整，未写入 Excel。");
                return row;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            { throw new TimeoutException("API 查询超时，未写入 Excel，请重试。"); }
        }

        public static string ValidateSn(string sn)
        {
            sn = (sn ?? "").Trim();
            if (sn.Length == 0 || sn.Length > 100 || sn.Any(c => char.IsWhiteSpace(c) || char.IsControl(c) || ",，;；".Contains(c))) throw new ArgumentException("请输入一个 SN。每次只查询一台机器。");
            return sn;
        }
        private static string Text(JObject value, string field) => value[field]?.Type == JTokenType.Null ? "" : ((string)value[field] ?? "").Trim();

        private async Task<JToken> PostAsync(BluettiSession session, string path, JObject payload, CancellationToken token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "https://gwpry.bluettipower.com" + path);
            request.Headers.Add("Authorization", session.Token);
            request.Headers.Add("x-app-resource", session.SiteId);
            request.Headers.Add("x-app-key", "18880A4BA6CF791157564C2ECD0");
            request.Headers.Add("x-os", "web"); request.Headers.Add("Accept-Language", "zh-CN");
            request.Headers.Add("Origin", "https://after-sales.bluettipower.com"); request.Headers.Referrer = new Uri("https://after-sales.bluettipower.com/");
            if (payload != null) request.Content = new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");
            using var response = await _client.SendAsync(request, token);
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden) throw new BluettiAuthenticationException();
            response.EnsureSuccessStatusCode();
            var envelope = JObject.Parse(await response.Content.ReadAsStringAsync(token));
            int? code = (int?)envelope["msgCode"];
            if (code >= 800 && code <= 999) throw new BluettiAuthenticationException();
            if (code != 0) throw new InvalidDataException("BLUETTI API 查询失败，代码：" + code);
            return envelope["data"];
        }
    }

    public sealed class BluettiLabelCaptureService
    {
        private readonly IBluettiLabelLookup _lookup;
        public BluettiLabelCaptureService(IBluettiLabelLookup lookup = null) { _lookup = lookup ?? new BluettiLabelApi(); }
        public async Task<BluettiLabelRow> CaptureAsync(BluettiSession session, string sn, DateTime date, string path, CancellationToken token)
        {
            var row = await _lookup.QueryAsync(session, sn, token);
            token.ThrowIfCancellationRequested();
            if (row == null) return null; // A miss never creates or changes a workbook.
            row.Date = date.ToString("yyyy/MM/dd");
            await Task.Run(() => { token.ThrowIfCancellationRequested(); BluettiLabelRow.Append(path, row); }, token);
            return row;
        }
    }
}
