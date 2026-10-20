using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace Scanner.Web.Services;

public sealed record TencentDocsRecordPage(string FileId, string SheetId, JsonArray Records, bool HasMore, int Next, int? Total);
public sealed record TencentDocsConnectionStatus(bool Enabled, bool Configured, bool Connected,
    string RedirectUri, string EncodedId, string SheetId, DateTimeOffset? ExpiresAt, string AuthMode = "OAuth");

public sealed class TencentDocsService(IOptions<TencentDocsOptions> settings, IHttpClientFactory clients,
    TencentDocsTokenStore store)
{
    public const string HttpClientName = "TencentDocs";
    private readonly TencentDocsOptions options = settings.Value;
    private readonly ConcurrentDictionary<string, DateTimeOffset> pendingStates = new();
    private string? resolvedFileId;

    public async Task<TencentDocsConnectionStatus> StatusAsync(CancellationToken ct)
    {
        if (!options.IsConfigured)
            return new(options.Enabled, false, false, options.RedirectUri, options.EncodedId, options.SheetId, null, options.AuthMode);
        if (options.UsesDirectToken)
            return new(true, true, options.AccessTokenExpiresAt is null || options.AccessTokenExpiresAt > DateTimeOffset.UtcNow,
                "", options.EncodedId, options.SheetId, options.AccessTokenExpiresAt, "DirectToken");
        await using var lease = await store.AcquireAsync(ct);
        var tokens = await store.ReadAsync(ct);
        var connected = tokens?.ClientId == options.ClientId;
        return new(options.Enabled, true, connected, options.RedirectUri, options.EncodedId, options.SheetId,
            connected ? tokens!.ExpiresAt : null);
    }

    public (string Url, string State) BeginAuthorization()
    {
        RequireConfiguration();
        if (options.UsesDirectToken) throw new ApiException(409, "当前使用直接凭据模式，请在服务器配置中更新 AccessToken 和 OpenId，无需发起 OAuth。");
        var now = DateTimeOffset.UtcNow;
        foreach (var pair in pendingStates)
            if (pair.Value < now) pendingStates.TryRemove(pair.Key, out _);
        if (pendingStates.Count >= 100) throw new ApiException(429, "待完成的授权过多，请稍后再试。");
        var state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        pendingStates[state] = now.AddMinutes(10);
        var url = QueryHelpers.AddQueryString("https://docs.qq.com/oauth/v2/authorize", new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId, ["redirect_uri"] = options.RedirectUri,
            ["new_login"] = "1", ["response_type"] = "code", ["scope"] = "all", ["state"] = state
        });
        return (url, state);
    }

    public bool ConsumeState(string? state, string? browserState)
    {
        if (string.IsNullOrEmpty(state) || string.IsNullOrEmpty(browserState) || state.Length > 100
            || state != browserState) return false;
        return pendingStates.TryRemove(state, out var expires) && expires > DateTimeOffset.UtcNow;
    }

    public async Task CompleteAuthorizationAsync(string code, CancellationToken ct)
    {
        RequireConfiguration();
        if (string.IsNullOrWhiteSpace(code) || code.Length > 4096) throw new ApiException(400, "授权码无效，请重新发起授权。");
        await using var lease = await store.AcquireAsync(ct);
        var data = await TokenRequestAsync(new Dictionary<string, string?>
        {
            ["grant_type"] = "authorization_code", ["redirect_uri"] = options.RedirectUri, ["code"] = code
        }, ct);
        await store.SaveAsync(ParseTokens(data, null), ct);
        resolvedFileId = null;
    }

    public async Task<JsonNode> GetSheetsAsync(CancellationToken ct)
    {
        RequireConfiguration();
        await using var lease = await store.AcquireAsync(ct);
        var tokens = await ValidTokensAsync(ct);
        var fileId = await ResolveFileIdAsync(tokens, ct);
        return await ApiRequestAsync(HttpMethod.Get, $"/openapi/smartbook/v2/files/{Uri.EscapeDataString(fileId)}/sheets", null, tokens, ct);
    }

    public async Task<TencentDocsRecordPage> GetRecordsAsync(int offset, int limit, CancellationToken ct)
    {
        RequireConfiguration();
        if (offset < 0 || limit is < 1 or > 100) throw new ApiException(400, "offset 不能小于 0，limit 必须为 1–100。");
        if (string.IsNullOrWhiteSpace(options.SheetId)) throw new ApiException(503, "请配置 TencentDocs:SheetId。");
        await using var lease = await store.AcquireAsync(ct);
        var tokens = await ValidTokensAsync(ct);
        var fileId = await ResolveFileIdAsync(tokens, ct);
        var data = await ApiRequestAsync(HttpMethod.Post,
            $"/openapi/smartbook/v2/files/{Uri.EscapeDataString(fileId)}/sheets/{Uri.EscapeDataString(options.SheetId)}",
            new { getRecords = new { offset, limit } }, tokens, ct);
        var page = data["getRecords"];
        if (page?["records"] is not JsonArray records || page["hasMore"] is null)
            throw new ApiException(502, "腾讯文档未返回预期的记录结构；请确认该文档类型支持当前开放接口。");
        var more = page["hasMore"]!.GetValue<bool>();
        var next = page["next"]?.GetValue<int>() ?? 0;
        if (more && (next <= offset || records.Count == 0)) throw new ApiException(502, "腾讯文档返回无效分页位置。");
        return new(fileId, options.SheetId, records, more, next, page["total"]?.GetValue<int>());
    }

    public async Task<JsonObject> UpdateRecordAsync(string recordId, JsonObject values, CancellationToken ct)
    {
        RequireConfiguration();
        if (string.IsNullOrWhiteSpace(recordId) || values.Count == 0) throw new ApiException(400, "推送记录或字段为空。");
        await using var lease = await store.AcquireAsync(ct);
        var tokens = await ValidTokensAsync(ct);
        var fileId = await ResolveFileIdAsync(tokens, ct);
        var data = await ApiRequestAsync(HttpMethod.Post,
            $"/openapi/smartbook/v2/files/{Uri.EscapeDataString(fileId)}/sheets/{Uri.EscapeDataString(options.SheetId)}",
            new { updateRecords = new { records = new[] { new { recordID = recordId, values } } } }, tokens, ct);
        var returned = (data["updateRecords"]?["records"] as JsonArray)?.FirstOrDefault(x => x?["recordID"]?.ToString() == recordId);
        if (returned?["values"] is not JsonObject confirmed || values.Any(pair => !confirmed.ContainsKey(pair.Key)
            || !BreezaryMerge.Equivalent(confirmed[pair.Key], pair.Value)))
            throw new ApiException(502, "腾讯文档未确认记录更新，改动仍保留为待推送，请稍后核对重试。");
        return (JsonObject)confirmed.DeepClone();
    }

    private async Task<TencentDocsTokens> ValidTokensAsync(CancellationToken ct)
    {
        if (options.UsesDirectToken)
        {
            if (options.AccessTokenExpiresAt <= DateTimeOffset.UtcNow)
                throw new ApiException(409, "腾讯文档访问令牌已过期，请在开发者平台重新生成，并更新服务器 AccessToken 和 OpenId。");
            return new(options.ClientId, options.AccessToken, "", options.OpenId,
                options.AccessTokenExpiresAt ?? DateTimeOffset.MaxValue, DateTimeOffset.UtcNow);
        }
        var saved = await store.ReadAsync(ct);
        if (saved is null || saved.ClientId != options.ClientId) throw new ApiException(409, "尚未连接腾讯文档，请管理员先完成授权。");
        if (saved.ExpiresAt > DateTimeOffset.UtcNow.AddMinutes(5)) return saved;
        var data = await TokenRequestAsync(new Dictionary<string, string?>
        {
            ["grant_type"] = "refresh_token", ["refresh_token"] = saved.RefreshToken
        }, ct);
        var refreshed = ParseTokens(data, saved);
        await store.SaveAsync(refreshed, ct);
        return refreshed;
    }

    private TencentDocsTokens ParseTokens(JsonNode data, TencentDocsTokens? previous)
    {
        var access = data["access_token"]?.ToString();
        var refresh = data["refresh_token"]?.ToString() ?? previous?.RefreshToken;
        var openId = data["user_id"]?.ToString() ?? previous?.OpenId;
        if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh) || string.IsNullOrWhiteSpace(openId)
            || !long.TryParse(data["expires_in"]?.ToString(), out var seconds) || seconds <= 0 || seconds > 31536000)
            throw new ApiException(502, "腾讯文档返回的授权信息不完整，请重新授权。");
        return new(options.ClientId, access, refresh, openId, DateTimeOffset.UtcNow.AddSeconds(seconds),
            previous?.ConnectedAt ?? DateTimeOffset.UtcNow);
    }

    private async Task<JsonNode> TokenRequestAsync(Dictionary<string, string?> parameters, CancellationToken ct)
    {
        parameters["client_id"] = options.ClientId;
        parameters["client_secret"] = options.ClientSecret;
        // Tencent's documented token endpoint uses GET. This client's HTTP logging is disabled.
        var url = QueryHelpers.AddQueryString("https://docs.qq.com/oauth/v2/token", parameters);
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        var result = await SendAsync(request, ct);
        return result["data"] ?? result;
    }

    private async Task<string> ResolveFileIdAsync(TencentDocsTokens tokens, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(options.FileId)) return options.FileId;
        if (resolvedFileId is not null) return resolvedFileId;
        if (string.IsNullOrWhiteSpace(options.EncodedId)) throw new ApiException(503, "请配置 TencentDocs:EncodedId 或 FileId。");
        var data = await ApiRequestAsync(HttpMethod.Get,
            "/openapi/drive/v2/util/converter?type=2&value=" + Uri.EscapeDataString(options.EncodedId), null, tokens, ct);
        var fileId = data["fileID"]?.ToString();
        if (string.IsNullOrWhiteSpace(fileId)) throw new ApiException(502, "腾讯文档未返回 fileID，无法读取表格。");
        return resolvedFileId = fileId;
    }

    private async Task<JsonNode> ApiRequestAsync(HttpMethod method, string path, object? body,
        TencentDocsTokens tokens, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, "https://docs.qq.com" + path);
        request.Headers.Add("Access-Token", tokens.AccessToken);
        request.Headers.Add("Client-Id", options.ClientId);
        request.Headers.Add("Open-Id", tokens.OpenId);
        if (body is not null) request.Content = JsonContent.Create(body);
        var response = await SendAsync(request, ct);
        return response["data"] ?? throw new ApiException(502, "腾讯文档响应缺少 data。");
    }

    private async Task<JsonNode> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        try
        {
            using var client = clients.CreateClient(HttpClientName);
            request.Headers.Accept.ParseAdd("application/json");
            using var response = await client.SendAsync(request, ct);
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ApiException(502, "腾讯文档拒绝访问，请检查应用权限、授权回调配置或重新授权。");
            if (response.StatusCode == HttpStatusCode.TooManyRequests) throw new ApiException(503, "腾讯文档接口调用受限，请稍后重试。");
            if (!response.IsSuccessStatusCode) throw new ApiException(502, $"腾讯文档请求失败（HTTP {(int)response.StatusCode}）。");
            var result = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            if (result is not JsonObject) throw new ApiException(502, "腾讯文档返回了无效响应。");
            var code = result["ret"] ?? result["error"];
            if (code is not null && code.ToString() != "0" && code.ToString() != "")
            {
                // Do not expose provider messages: token endpoints can echo credentials or authorization codes.
                var safeCode = new string(code.ToString().Where(c => char.IsAsciiLetterOrDigit(c) || c == '_').Take(60).ToArray());
                throw new ApiException(502, $"腾讯文档接口错误（{safeCode}）。请检查应用权限、文档类型及授权是否有效。");
            }
            return result;
        }
        catch (HttpRequestException) { throw new ApiException(502, "无法连接腾讯文档，请检查服务器网络。"); }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(504, "腾讯文档接口响应超时，请稍后重试。"); }
        catch (System.Text.Json.JsonException) { throw new ApiException(502, "腾讯文档返回了无法解析的响应。"); }
    }

    private void RequireConfiguration()
    {
        if (!options.IsConfigured) throw new ApiException(503, "请配置 TencentDocs：DirectToken 模式填写 ClientId、AccessToken、OpenId；OAuth 模式填写 ClientId、ClientSecret 和 HTTPS RedirectUri。");
    }
}
