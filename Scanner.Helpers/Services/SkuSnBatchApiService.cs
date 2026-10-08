using Newtonsoft.Json;
using System.Net.Http;
namespace Scanner.Helpers.Services
{
    public sealed class SkuSnBatchInfo
    {
        public Guid BatchId { get; set; }
        public string BatchNumber { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public int ItemCount { get; set; }
        public List<SkuSnPair> Items { get; set; }
        public override string ToString() => BatchNumber + " · " + ItemCount + " 条 · " + BatchId.ToString("D");
    }
    public sealed class SkuSnPair { public string Sku { get; set; } public string Sn { get; set; } }
    public sealed partial class ShelvedPalletApiService
    {
        private async Task<T> DownloadBatchJsonAsync<T>(string path, string key)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, path))
            {
                if (!string.IsNullOrWhiteSpace(key)) request.Headers.Add("X-Upload-Key",key.Trim());
                using (var response = await _client.SendAsync(request).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) throw new InvalidOperationException("批次下载失败 HTTP " + (int)response.StatusCode);
                    var value = JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
                    if (value == null) throw new InvalidOperationException("批次响应无效。");
                    return value;
                }
            }
        }
        public Task<List<SkuSnBatchInfo>> GetSkuSnBatchesAsync(string key) => DownloadBatchJsonAsync<List<SkuSnBatchInfo>>("api/sku-sn-batches",key);
        public Task<SkuSnBatchInfo> GetSkuSnBatchAsync(Guid id,string key) => DownloadBatchJsonAsync<SkuSnBatchInfo>("api/sku-sn-batches/"+id.ToString("D"),key);
    }
}
