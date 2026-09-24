using Newtonsoft.Json;
using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.WPF.Services
{
    public sealed class ShelvedPalletUploadService
    {
        private static readonly HttpClient HttpClient = CreateHttpClient();

        public async Task<ShelvedPalletUploadResult> UploadAsync(string palletNumber, IEnumerable<OutboundInspectionRecord> records)
        {
            string pallet = (palletNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(pallet)) throw new ArgumentException("请输入托盘号。", nameof(palletNumber));
            List<OutboundInspectionRecord> source = (records ?? Enumerable.Empty<OutboundInspectionRecord>()).ToList();
            if (source.Count == 0) throw new InvalidOperationException("没有可上传的出库检测数据。");

            var payload = new
            {
                palletNumber = pallet,
                items = source.Select(item => new
                {
                    number = item.Number,
                    sn = item.Sn,
                    sku = item.Sku,
                    type = item.Type,
                    processingMethod = item.ProcessingMethod,
                    processingTime = item.ProcessingTime
                }).ToArray()
            };

            string json = JsonConvert.SerializeObject(payload);
            using (var request = new HttpRequestMessage(HttpMethod.Post, "api/shelved-pallets"))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                string uploadKey = ConfigurationManager.AppSettings["UploadApiKey"] ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(uploadKey)) request.Headers.Add("X-Upload-Key", uploadKey);

                try
                {
                    using (HttpResponseMessage response = await HttpClient.SendAsync(request).ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            ApiError error = TryDeserialize<ApiError>(body);
                            throw new InvalidOperationException(error == null || string.IsNullOrWhiteSpace(error.Message)
                                ? "上传服务返回错误：HTTP " + (int)response.StatusCode
                                : error.Message);
                        }

                        ShelvedPalletUploadResult result = TryDeserialize<ShelvedPalletUploadResult>(body);
                        if (result == null || result.InsertedCount <= 0) throw new InvalidOperationException("上传服务器没有返回有效结果。");
                        return result;
                    }
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException("无法连接上传服务器 " + HttpClient.BaseAddress + "，请检查网络和 UploadBaseUrl 配置。", ex);
                }
                catch (TaskCanceledException ex)
                {
                    throw new InvalidOperationException("连接上传服务器 " + HttpClient.BaseAddress + " 超时，请稍后重试。", ex);
                }
            }
        }

        private static HttpClient CreateHttpClient()
        {
            string baseUrl = (ConfigurationManager.AppSettings["UploadBaseUrl"] ?? "https://wms.ymforever.com/").Trim();
            if (!baseUrl.EndsWith("/", StringComparison.Ordinal)) baseUrl += "/";
            return new HttpClient { BaseAddress = new Uri(baseUrl, UriKind.Absolute), Timeout = TimeSpan.FromSeconds(60) };
        }

        private static T TryDeserialize<T>(string json) where T : class
        {
            try { return JsonConvert.DeserializeObject<T>(json); }
            catch (JsonException) { return null; }
        }

        private sealed class ApiError { public string Message { get; set; } }
    }

    public sealed class ShelvedPalletUploadResult
    {
        public string PalletNumber { get; set; }
        public DateTime ShelvedAt { get; set; }
        public int InsertedCount { get; set; }
        public IList<string> Uuids { get; set; }
    }
}

