using System.Data;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ScannerProject.BreezaryStar.WPF.Services;

/// <summary>The desktop only authenticates to Scanner.Web; Tencent credentials stay on the server.</summary>
public sealed class ScannerServerReader(HttpClient http)
{
    public static Uri ServerUri(string value)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != "https" && !(uri.Scheme == "http" && uri.IsLoopback))
            || !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment) || uri.AbsolutePath != "/")
            throw new InvalidOperationException("请输入 Scanner 服务器的 HTTPS 根地址，例如 https://wms.ymforever.com；本机测试可用 HTTP。");
        return uri;
    }

    public async Task<DataTable> ReadAsync(Uri server, string username, string password,
        IProgress<string>? progress, CancellationToken ct)
    {
        ServerUri(server.ToString());
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new InvalidOperationException("请输入 Scanner 管理员账号和密码，不是腾讯文档 Token。");
        string? session = null;
        try
        {
            progress?.Report("正在登录 Scanner 服务器…");
            var account = await RequestAsync(server, "api/account/login", HttpMethod.Post,
                new { username = username.Trim(), password }, null, ct);
            session = account["token"]?.ToString();
            if (string.IsNullOrWhiteSpace(session)) throw new InvalidOperationException("服务器登录响应缺少会话令牌。");
            if (account["permissions"] is not JsonArray permissions || !permissions.Any(p => p?.ToString() == "system.configure"))
                throw new InvalidOperationException("当前测试接口要求 Scanner 管理员权限。");
            // Local reads remain available when Tencent is offline or its token has expired.
            var records = new DataTable();
            records.Columns.Add("记录 ID");
            var ids = new HashSet<string>();
            var offset = 0;
            while (true)
            {
                var page = await RequestAsync(server, $"api/tencent-docs/records?offset={offset}&limit=100",
                    HttpMethod.Get, null, session, ct);
                if (page["records"] is not JsonArray rows || page["hasMore"] is null)
                    throw new InvalidOperationException("服务器未返回预期的记录列表，请确认已部署最新 Scanner.Web。");
                foreach (var record in rows)
                {
                    var id = (record?["recordID"] ?? record?["record_id"])?.ToString();
                    if (string.IsNullOrEmpty(id) || !ids.Add(id)) throw new InvalidOperationException("记录 ID 缺失或重复，请重新拉取。");
                    if ((record?["values"] ?? record?["field_values"]) is not JsonObject values)
                        throw new InvalidOperationException("服务器返回的记录缺少 values 字段。");
                    foreach (var pair in values)
                        if (!records.Columns.Contains(pair.Key)) records.Columns.Add(pair.Key);
                    var row = records.NewRow();
                    row["记录 ID"] = id;
                    foreach (var pair in values) row[pair.Key] = TencentDocsReader.CellText(pair.Value);
                    records.Rows.Add(row);
                }
                progress?.Report($"已从服务器读取 {records.Rows.Count} 条记录…");
                if (!page["hasMore"]!.GetValue<bool>()) return records;
                var next = page["next"]?.GetValue<int>() ?? 0;
                if (next <= offset || rows.Count == 0) throw new InvalidOperationException("服务器返回无效分页位置。");
                if (records.Rows.Count >= 10000) throw new InvalidOperationException("测试窗口最多读取 10000 条记录，请缩小数据范围。");
                offset = next;
            }
        }
        finally
        {
            if (!string.IsNullOrEmpty(session))
            {
                // This session exists only for this read. Close it even when the read fails or is cancelled.
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try { await RequestAsync(server, "api/account/logout", HttpMethod.Post, null, session, cleanup.Token); }
                catch { /* Expiration on Scanner.Web remains the fallback if the server is unreachable. */ }
            }
        }
    }

    private async Task<JsonNode> RequestAsync(Uri server, string path, HttpMethod method, object? body,
        string? session, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, new Uri(server, path));
        if (body is not null) request.Content = JsonContent.Create(body);
        if (session is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session);
        using var response = await http.SendAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
            throw new InvalidOperationException("服务器没有此接口，请先部署包含腾讯文档 OAuth 接入的新版 Scanner.Web。");
        if ((int)response.StatusCode is >= 300 and < 400)
            throw new InvalidOperationException("服务器返回重定向，请直接填写最终 HTTPS 站点地址。");
        var text = await response.Content.ReadAsStringAsync(ct);
        JsonNode? result;
        try { result = JsonNode.Parse(text); }
        catch (JsonException) { throw new InvalidOperationException($"服务器返回非 JSON 响应（HTTP {(int)response.StatusCode}），请检查地址和部署版本。"); }
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException(result?["message"]?.ToString() ?? $"服务器请求失败（HTTP {(int)response.StatusCode}）。");
        return result as JsonObject ?? throw new InvalidOperationException("服务器响应格式不正确。");
    }
}
