using System.Net.Http.Json;
using System.Text.Json;
using Scanner.Server.Model;
namespace Scanner.AndroidTester.Services;

public sealed class LocationScanApiService(HttpClient client)
{
    public async Task UploadAsync(IReadOnlyList<LocationScanItem> items, string apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/warehouse-locations") { Content = JsonContent.Create(new LocationBatchRequest(items)) };
        if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.Add("X-Upload-Key", apiKey.Trim());
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            string message = $"上传失败（HTTP {(int)response.StatusCode}），未确认的记录已保留。";
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) message = "上传密钥无效，请检查密钥后重试。";
            else
            {
                try
                {
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (json.RootElement.TryGetProperty("message", out var detail) && detail.ValueKind == JsonValueKind.String) message = detail.GetString()!;
                }
                catch (JsonException) { }
            }
            throw new InvalidOperationException(message);
        }
        var result = await response.Content.ReadFromJsonAsync<LocationBatchResult>();
        if (result?.AcceptedLocationIds is null || result.AcceptedLocationIds.Count != items.Count ||
            !result.AcceptedLocationIds.ToHashSet().SetEquals(items.Select(x => x.LocationId)))
            throw new InvalidOperationException("服务器未确认全部扫描记录，已保留待上传数据，可重试。");
    }
}

