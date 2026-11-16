using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace Scanner.Web.Services;

/// <summary>Ports the WPF login/site/export protocol without desktop storage or browser dependencies.</summary>
public sealed class BluettiReceivingClient(HttpClient http)
{
    private static HttpRequestMessage Request(HttpMethod method, string path, string? token = null, string? site = null)
    {
        var request = new HttpRequestMessage(method, "https://gwpry.bluettipower.com" + path);
        request.Headers.Add("x-app-key", "18880A4BA6CF791157564C2ECD0");
        request.Headers.Add("x-os", "web"); request.Headers.Add("Accept-Language", "zh-CN");
        if (token != null) request.Headers.Add("Authorization", token);
        if (site != null) request.Headers.Add("x-app-resource", site);
        return request;
    }

    private async Task<JsonNode> Json(HttpRequestMessage request, CancellationToken ct, bool allowEmpty = false)
    {
        using (request)
        using (var response = await http.SendAsync(request, ct))
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden) throw new ApiException(502, "BLUETTI 登录或站点权限被拒绝。");
            if (!response.IsSuccessStatusCode) throw new ApiException(502, $"BLUETTI 请求失败（HTTP {(int)response.StatusCode}）。");
            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync(ct));
            if (json?["msgCode"]?.GetValue<int>() != 0) throw new ApiException(502, "BLUETTI 登录失效、权限不足或请求被拒绝。");
            return json["data"] ?? (allowEmpty ? JsonValue.Create(true)! : throw new InvalidDataException("BLUETTI 响应缺少数据。"));
        }
    }

    public async Task<IReadOnlyList<BluettiForecast>> Fetch(string username, string password, string site, CancellationToken ct)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(username.Trim()), "username");
        form.Add(new StringContent(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)))), "password");
        var login = Request(HttpMethod.Post, "/accessToken"); login.Content = form;
        string token = (await Json(login, ct))["token"]?.GetValue<string>() ?? throw new InvalidDataException("BLUETTI 未返回登录令牌。");
        var user = await Json(Request(HttpMethod.Get, "/api/userInfo", token), ct);
        string uid = user["uid"]?.ToString() ?? throw new InvalidDataException("BLUETTI 未返回用户信息。");
        var sites = await Json(Request(HttpMethod.Get, "/api/bluas/client/user/v1/getUserSite?uid=" + Uri.EscapeDataString(uid), token), ct);
        if (sites is not JsonArray array || !array.Any(s => s?["siteId"]?.ToString() == site && s?["enable"]?.ToString() != "0"))
            throw new ApiException(502, "配置的 BLUETTI 站点不在账号可用站点列表中。");
        await Json(Request(HttpMethod.Get, "/api/bluas/client/role/v1/existPermissions", token, site), ct, allowEmpty: true);
        using var request = Request(HttpMethod.Post, "/api/bluas/client/workOrder/postSale/exportCustomerDemands", token, site);
        request.Headers.Add("Origin", "https://after-sales.bluettipower.com"); request.Headers.Referrer = new Uri("https://after-sales.bluettipower.com/");
        request.Content = new StringContent(Payload(site).ToJsonString(), Encoding.UTF8, "application/json");
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode) throw new ApiException(502, $"BLUETTI 预报导出失败（HTTP {(int)response.StatusCode}）。");
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream(); var chunk = new byte[81920]; int count;
        while ((count = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + count > 50_000_000) throw new InvalidDataException("BLUETTI 导出文件超过 50MB。");
            buffer.Write(chunk, 0, count);
        }
        if (buffer.Length < 2 || buffer.GetBuffer()[0] != 'P' || buffer.GetBuffer()[1] != 'K') throw new ApiException(502, "BLUETTI 未返回有效预报工作簿，请检查登录、站点或导出权限。");
        buffer.Position = 0;
        return BluettiForecastParser.Parse(buffer);
    }

    public static JsonObject Payload(string site)
    {
        var body = new JsonObject();
        foreach (string key in new[] { "purchaseChannels", "isOverseasReceiving", "shippingOrderNo", "sn", "status", "afterStatus", "postSaleNo", "handler", "customerPhone", "customerName", "customerEmail", "afterSaleSource", "returnReason", "orderNo", "model", "rmaNo", "customerType", "afterSaleReason", "ticketId", "creBy", "orderByField", "orderBy" }) body[key] = "";
        foreach (string key in new[] { "submitTime", "afterStatusList", "afterSaleTypeList", "dataSourceList", "idList" }) body[key] = new JsonArray();
        body["type"] = 1; body["state"] = 1; body["pageNo"] = 1; body["pageSize"] = 20; body["siteId"] = site;
        return body;
    }
}
