using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Xml.Linq;
using Scanner.Server.Model;

namespace WPF.Camara;

public sealed class RepairStartApi(HttpClient client)
{
    public async Task UploadAsync(Uri baseUrl, string apiKey, RepairScan scan, CancellationToken ct = default)
    {
        if (!baseUrl.IsAbsoluteUri || baseUrl.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(baseUrl.UserInfo)
            || !string.IsNullOrEmpty(baseUrl.Query) || !string.IsNullOrEmpty(baseUrl.Fragment))
            throw new ArgumentException("请输入有效的 HTTPS 服务地址。请包含末尾斜杠。测试服务请使用 HTTPS。");
        if (string.IsNullOrWhiteSpace(apiKey)) throw new ArgumentException("请配置上传密钥。");
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(baseUrl.AbsoluteUri.TrimEnd('/') + "/"), "api/repair-scans"))
            { Content = JsonContent.Create(scan) };
        request.Headers.Add("X-Upload-Key", apiKey.Trim());
        using var response = await client.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => "上传密钥无效，请检查配置后重试。",
                System.Net.HttpStatusCode.Conflict => "扫描编号与服务器记录冲突，请保留本地记录并联系管理员。",
                System.Net.HttpStatusCode.BadRequest => "服务器拒绝该记录，请检查编码和设备时间。记录已保留。",
                _ => $"上传失败（HTTP {(int)response.StatusCode}）。请检查服务、建表脚本及网络后重试。"
            });
        using var result = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        if (!result.RootElement.TryGetProperty("scanId", out var id) || !id.TryGetGuid(out var confirmed) || confirmed != scan.ScanId
            || !result.RootElement.TryGetProperty("status", out var status) || status.GetString() != "开始维修")
            throw new InvalidOperationException("服务器未正确确认本条开始维修记录，请重试。");
    }

    public static (string BaseUrl, string ApiKey) LoadSettings()
    {
        var path = System.IO.Path.Combine(AppContext.BaseDirectory, "repair-upload.xml");
        var root = System.IO.File.Exists(path) ? XDocument.Load(path).Root : null;
        return (Environment.GetEnvironmentVariable("SCANNER_UPLOAD_BASE_URL") ?? root?.Element("BaseUrl")?.Value ?? "https://wms.ymforever.com/",
            Environment.GetEnvironmentVariable("SCANNER_UPLOAD_API_KEY") ?? root?.Element("ApiKey")?.Value ?? "");
    }
}
