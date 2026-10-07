using Newtonsoft.Json;
using Scanner.Models;
using System.Text;
using System.Net.Http;

namespace Scanner.Helpers.Services
{
    public sealed partial class ShelvedPalletApiService
    {
        private readonly HttpClient _client;

        public ShelvedPalletApiService(HttpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public async Task<IReadOnlyList<WarehouseInventoryRow>> GetWarehouseInventoryAsync(string apiKey = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "api/shelved-pallets/all"))
            {
                if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.Add("X-Upload-Key", apiKey.Trim());
                using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode) throw new InvalidOperationException("导入 Web 上架表失败：HTTP " + (int)response.StatusCode);
                    var table = Newtonsoft.Json.Linq.JObject.Parse(body);
                    var rows = table["rows"] as Newtonsoft.Json.Linq.JArray ?? throw new InvalidOperationException("Web 没有返回有效的上架数据表。");
                    var result = new List<WarehouseInventoryRow>();
                    foreach (Newtonsoft.Json.Linq.JObject row in rows)
                    {
                        DateTime date;
                        string dateText = (string)row["shelving_date"];
                        DateTime? shelving = null;
                        if (!string.IsNullOrWhiteSpace(dateText))
                        {
                            if (!DateTime.TryParse(dateText, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date)) throw new InvalidOperationException("Web 上架日期格式无效。");
                            shelving = date.Date;
                        }
                        result.Add(new WarehouseInventoryRow { Sn = (string)row["sn"], Sku = (string)row["sku"], PalletNumber = (string)row["pallet_number"], ShelvingDate = shelving });
                    }
                    return result.AsReadOnly();
                }
            }
        }

        public async Task<IReadOnlyList<PdaPalletRow>> GetPdaPalletsAsync(string apiKey = null)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, "api/tester-pallet-scans"))
            {
                if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.Add("X-Upload-Key", apiKey.Trim());
                using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        var error = Deserialize<ApiErrorResponse>(body);
                        string detail = error?.Message;
                        if (string.IsNullOrWhiteSpace(detail))
                            detail = response.StatusCode == System.Net.HttpStatusCode.NotFound
                                ? "请确认服务器已部署自动匹配查询接口。"
                                : "请检查服务器运行状态和访问配置。";
                        throw new InvalidOperationException("获取 PDA 托盘数据失败：HTTP " + (int)response.StatusCode + "。" + detail);
                    }
                    return JsonConvert.DeserializeObject<List<PdaPalletRow>>(body)?.AsReadOnly()
                        ?? throw new InvalidOperationException("服务器没有返回有效的 PDA 托盘数据。");
                }
            }
        }

        public async Task<ShelvedPalletUploadResult> UploadAsync(string palletNumber, IEnumerable<OutboundInspectionRecord> records, string apiKey = null)
        {
            string pallet = (palletNumber ?? string.Empty).Trim();
            if (pallet.Length == 0) throw new ArgumentException("请输入托盘号。", nameof(palletNumber));
            List<OutboundInspectionRecord> source = (records ?? Enumerable.Empty<OutboundInspectionRecord>()).ToList();
            if (source.Count == 0) throw new InvalidOperationException("没有可上传的出库检测数据。");

            ShelvedPalletBatchRequest payload = new ShelvedPalletBatchRequest
            {
                PalletNumber = pallet,
                Items = source.Select(item => new ShelvedPalletItem
                {
                    Number = item.Number,
                    Sn = item.Sn,
                    Sku = item.Sku,
                    Type = item.Type,
                    ProcessingMethod = item.ProcessingMethod,
                    ProcessingTime = item.ProcessingTime
                }).ToList()
            };

            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "api/shelved-pallets"))
            {
                request.Content = new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");
                if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.Add("X-Upload-Key", apiKey.Trim());
                try
                {
                    using (HttpResponseMessage response = await _client.SendAsync(request).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            ApiErrorResponse error = Deserialize<ApiErrorResponse>(body);
                            throw new InvalidOperationException(string.IsNullOrWhiteSpace(error?.Message)
                                ? "上传服务返回错误：HTTP " + (int)response.StatusCode
                                : error.Message);
                        }
                        ShelvedPalletUploadResult result = Deserialize<ShelvedPalletUploadResult>(body);
                        return result != null && result.InsertedCount > 0
                            ? result
                            : throw new InvalidOperationException("上传服务器没有返回有效结果。");
                    }
                }
                catch (TaskCanceledException exception)
                {
                    throw new InvalidOperationException("连接上传服务器超时，请稍后重试。", exception);
                }
                catch (HttpRequestException exception)
                {
                    throw new InvalidOperationException("无法连接上传服务器，请检查网络和服务器地址。", exception);
                }
            }
        }

        private static T Deserialize<T>(string json) where T : class
        {
            try { return JsonConvert.DeserializeObject<T>(json); }
            catch (JsonException) { return null; }
        }
    }
}
