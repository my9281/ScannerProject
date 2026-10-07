using Newtonsoft.Json;
using Scanner.Models;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace Scanner.Helpers.Services
{
    public sealed class WorkOrderUploadResult
    {
        public int InsertedCount { get; set; }
        public int SkippedCount { get; set; }
        public ulong UserId { get; set; }
        public string Username { get; set; }
    }
    public sealed partial class ShelvedPalletApiService
    {
        public async Task<WorkOrderUploadResult> UploadMatchedWorkOrdersAsync(IReadOnlyList<AutomaticMatchRow> rows, string apiKey = null)
        {
            if (rows == null || rows.Count == 0) throw new ArgumentException("没有可上传的匹配结果。");
            var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
            var items = rows.Select(row =>
            {
                string description = $"托盘号：{row.PalletNumber}；SKU：{row.Sku}；RMA：{row.RmaNumber}；类型：{row.Type}；检测状态：{row.DetectionStatus}；扫描日期：{row.ScanDate:yyyy-MM-dd}；扫描时间：{row.ScanTime:yyyy-MM-dd HH:mm:ss}；处理时间：{row.ProcessingTime}";
                string source = JsonConvert.SerializeObject(row);
                occurrences.TryGetValue(source, out int ordinal);
                occurrences[source] = ordinal + 1;
                string key;
                using (var hash = SHA256.Create())
                    key = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes("automatic-match-v1:" + source + ":" + ordinal))).Replace("-", "").ToLowerInvariant();
                if (string.IsNullOrWhiteSpace(row.Sn) || row.Sn.Trim().Length > 100 || description.Length > 1000)
                    throw new ArgumentException("匹配结果设备 ID 或描述超出服务端长度限制，请检查基础表。");
                return new { sourceKey = key, deviceId = row.Sn.Trim(), description };
            }).ToList();
            var total = new WorkOrderUploadResult();
            for (int offset = 0; offset < items.Count; offset += 500)
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, "api/checklist-work-orders/batch"))
                {
                    request.Content = new StringContent(JsonConvert.SerializeObject(new { items = items.Skip(offset).Take(500).ToArray() }), Encoding.UTF8, "application/json");
                    if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.Add("X-Upload-Key", apiKey.Trim());
                    try
                    {
                        using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                        {
                            string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!response.IsSuccessStatusCode)
                                throw new InvalidOperationException(Deserialize<ApiErrorResponse>(body)?.Message ?? "工单上传失败：HTTP " + (int)response.StatusCode);
                            var result = Deserialize<WorkOrderUploadResult>(body);
                            if (result == null || result.UserId == 0 || result.Username != "my9281" || result.InsertedCount < 0 || result.SkippedCount < 0 || result.InsertedCount + result.SkippedCount != Math.Min(500, items.Count - offset))
                                throw new InvalidOperationException("工单上传返回结果无效，请重试。");
                            total.InsertedCount += result.InsertedCount;
                            total.SkippedCount += result.SkippedCount;
                            total.UserId = result.UserId;
                            total.Username = result.Username;
                        }
                    }
                    catch (TaskCanceledException ex) { throw new InvalidOperationException("工单上传超时，可点击重试上传；已成功上传的工单会自动跳过。", ex); }
                    catch (HttpRequestException ex) { throw new InvalidOperationException("工单上传连接失败，可检查网络后重试上传。", ex); }
                }
            }
            return total;
        }
    }
}
