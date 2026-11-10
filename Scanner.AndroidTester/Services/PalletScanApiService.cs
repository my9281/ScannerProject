using System.Net.Http.Json;
using System.Text.Json;
using Scanner.Server.Model;
namespace Scanner.AndroidTester.Services;

public sealed class PalletScanApiService(HttpClient client)
{
    public async Task<PalletScanStoredRow?> LookupAsync(string sn, string apiKey, CancellationToken cancellationToken = default)
    {
        sn = sn?.Trim() ?? string.Empty;
        if (sn.Length is < 1 or > 100) throw new ArgumentException(UiText.T("SN 必须为 1–100 个字符。"));
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/tester-pallet-scans/lookup?sn=" + Uri.EscapeDataString(sn));
        if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.Add("X-Upload-Key", apiKey.Trim());
        using var response = await client.SendAsync(request, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new InvalidOperationException(UiText.T("上传密钥无效，请检查密钥后重试。"));
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(UiText.F("查询失败（HTTP {0}）。", (int)response.StatusCode));
        var row = await response.Content.ReadFromJsonAsync<PalletScanStoredRow>(cancellationToken);
        if (row is null || row.PalletNumber is < 1 or > 100 || !string.Equals(row.Sn, sn, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(UiText.T("服务器返回的托盘信息无效，请重试。"));
        return row;
    }

    public async Task UploadAsync(IReadOnlyList<PalletScanItem> items, string apiKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/tester-pallet-scans") { Content = JsonContent.Create(new PalletScanBatchRequest(items)) };
        if (!string.IsNullOrWhiteSpace(apiKey)) request.Headers.Add("X-Upload-Key", apiKey.Trim());
        using var response = await client.SendAsync(request);
        if (!response.IsSuccessStatusCode)
        {
            string message = UiText.F("上传失败（HTTP {0}），未确认的记录已保留。", (int)response.StatusCode);
            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized) message = UiText.T("上传密钥无效，请检查密钥后重试。");
            else
            {
                try
                {
                    using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                    if (json.RootElement.TryGetProperty("message", out var detail) && detail.ValueKind == JsonValueKind.String) {
                        var serverMessage = detail.GetString()!;
                        if (!serverMessage.Any(c => c is >= '\u4E00' and <= '\u9FFF')) message = serverMessage;
                    }
                }
                catch (JsonException) { }
            }
            throw new InvalidOperationException(message);
        }
        var result = await response.Content.ReadFromJsonAsync<PalletScanBatchResult>();
        if (result?.AcceptedScanIds is null || result.AcceptedScanIds.Count != items.Count ||
            !result.AcceptedScanIds.ToHashSet().SetEquals(items.Select(x => x.ScanId)))
            throw new InvalidOperationException(UiText.T("服务器未确认全部扫描记录，已保留待上传数据，可重试。"));
    }
}
