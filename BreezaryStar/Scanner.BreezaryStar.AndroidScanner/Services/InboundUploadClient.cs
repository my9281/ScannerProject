using System.Net.Http.Json;
using System.Text.Json;
using Scanner.Server.Model;

namespace Scanner.BreezaryStar.AndroidScanner.Services;

public sealed class InboundUploadClient(HttpClient client)
{
    public static Uri Server(string address)
    {
        if (!Uri.TryCreate(address.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri) || uri.Scheme != "https"
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("请输入有效的 HTTPS 服务器地址。");
        return uri;
    }

    public async Task<int> UploadPendingAsync(InboundCache cache, string address, string apiKey, CancellationToken ct = default)
    {
        var server = Server(address);
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("请先在连接设置中填写上传密钥。");
        var pending = (await cache.SnapshotAsync(ct)).Where(x => x.UploadedAt is null).Select(x => x.Item).ToArray();
        int uploaded = 0;
        foreach (var batch in pending.Chunk(200))
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(server, "api/breezary-inbound-scans"));
            request.Headers.Add("X-Upload-Key", apiKey.Trim());
            request.Content = JsonContent.Create(new BreezaryInboundBatch(batch));
            using var response = await client.SendAsync(request, ct);
            var json = await response.Content.ReadAsStringAsync(ct);
            if (!response.IsSuccessStatusCode)
            {
                string message = $"上传失败（HTTP {(int)response.StatusCode}），未确认记录保留在本地。";
                try { using var error = JsonDocument.Parse(json); if (error.RootElement.TryGetProperty("message", out var text)) message = text.GetString() ?? message; }
                catch (JsonException) { }
                throw new InvalidOperationException(message);
            }
            var result = JsonSerializer.Deserialize<BreezaryInboundAcknowledgement>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
            if (result?.AcknowledgedIds is null || result.AcknowledgedIds.Count != batch.Length
                || !result.AcknowledgedIds.ToHashSet().SetEquals(batch.Select(x => x.ScanId)) || result.AcknowledgedAt == default)
                throw new InvalidOperationException("服务器确认列表不完整，当前批次仍保留为待上传。");
            await cache.AcknowledgeAsync(result.AcknowledgedIds, result.AcknowledgedAt, ct);
            uploaded += batch.Length;
        }
        return uploaded;
    }
}
