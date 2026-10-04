using Newtonsoft.Json;
using Scanner.Models;
using System.Text;
using System.Net.Http;

namespace Scanner.Helpers.Services
{
    public sealed class ShelvedPalletApiService
    {
        private readonly HttpClient _client;

        public ShelvedPalletApiService(HttpClient client)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
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
