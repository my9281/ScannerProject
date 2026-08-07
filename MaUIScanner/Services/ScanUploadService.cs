using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace MaUIScanner.Services;

public sealed class ScanUploadService
{
    private readonly HttpClient _client;
    public ScanUploadService(HttpClient client)
    {
        _client = client;
    }
    public async Task<ScanUploadResult> UploadAsync(string filePath)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("尚无可导出的扫描记录。", filePath);
        await using FileStream stream = File.OpenRead(filePath);
        using MultipartFormDataContent content = new();
        using StreamContent fileContent = new(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(fileContent, "file", $"scanned_codes_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        content.Add(new StringContent(DeviceInfo.Current.Name ?? "unknown"), "deviceName");
        string uploadKey = Preferences.Default.Get("UploadApiKey", string.Empty);
        using HttpRequestMessage request = new(HttpMethod.Post, "api/uploads/scans") { Content = content };
        if (!string.IsNullOrWhiteSpace(uploadKey)) request.Headers.Add("X-Upload-Key", uploadKey);
        using HttpResponseMessage response = await _client.SendAsync(request);
        ScanUploadResult? result = await response.Content.ReadFromJsonAsync<ScanUploadResult>();
        if (!response.IsSuccessStatusCode) throw new InvalidOperationException(result?.Message ?? $"服务器返回 HTTP {(int)response.StatusCode}。");
        return result ?? throw new InvalidOperationException("服务器没有返回上传结果。");
    }
}

public sealed class ScanUploadResult
{
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}
