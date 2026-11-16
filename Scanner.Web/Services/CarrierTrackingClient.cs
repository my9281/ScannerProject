using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Scanner.Web.Services;

public sealed record CarrierCredentials(bool Enabled, bool Sandbox, string ClientId, string Secret);
public sealed record CarrierTrackingResult(string Status, bool Delivered);

public sealed class CarrierTrackingClient(HttpClient http)
{
    public static string Carrier(string value) => value.ToLowerInvariant() switch
    {
        "fedex" => "fedex", "usps" => "usps", _ => throw new ArgumentException("仅支持 FedEx 和 USPS。")
    };

    public static string Number(string value)
    {
        value = value.Trim();
        if (value.Length is < 8 or > 40 || value.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("运单号须为 8–40 位英文字母或数字。");
        return value.ToUpperInvariant();
    }

    public async Task<CarrierTrackingResult> TrackAsync(string carrier, string number, CarrierCredentials config, CancellationToken ct)
    {
        carrier = Carrier(carrier); number = Number(number);
        if (!config.Enabled || string.IsNullOrWhiteSpace(config.ClientId) || string.IsNullOrWhiteSpace(config.Secret))
            throw new ApiException(409, "请先在管理员界面填写凭据并启用该承运商。");
        string host = carrier == "fedex"
            ? (config.Sandbox ? "https://apis-sandbox.fedex.com" : "https://apis.fedex.com")
            : (config.Sandbox ? "https://apis-tem.usps.com" : "https://apis.usps.com");
        try
        {
            using var auth = new HttpRequestMessage(HttpMethod.Post, host + (carrier == "fedex" ? "/oauth/token" : "/oauth2/v3/token"));
            auth.Content = carrier == "fedex"
                ? new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials", ["client_id"] = config.ClientId, ["client_secret"] = config.Secret })
                : JsonContent.Create(new { grant_type = "client_credentials", client_id = config.ClientId, client_secret = config.Secret });
            using var authorized = await http.SendAsync(auth, ct);
            if (!authorized.IsSuccessStatusCode) throw Failure(carrier, "授权", (int)authorized.StatusCode);
            var token = JsonNode.Parse(await authorized.Content.ReadAsStringAsync(ct))?["access_token"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(token)) throw new ApiException(502, "承运商未返回授权令牌。");
            using var request = new HttpRequestMessage(HttpMethod.Post, host + (carrier == "fedex" ? "/track/v1/trackingnumbers" : "/tracking/v3r2/tracking"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = carrier == "fedex"
                ? JsonContent.Create(new { includeDetailedScans = true, trackingInfo = new[] { new { trackingNumberInfo = new { trackingNumber = number } } } })
                : JsonContent.Create(new[] { new { trackingNumber = number } });
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) throw Failure(carrier, "查询", (int)response.StatusCode);
            return Parse(carrier, JsonNode.Parse(await response.Content.ReadAsStringAsync(ct))!);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new ApiException(504, "承运商查询超时，请重试。"); }
        catch (HttpRequestException) { throw new ApiException(502, "无法连接承运商，请稍后重试。"); }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or FormatException)
        { throw new ApiException(502, "承运商返回的数据格式无法识别。"); }
    }

    private static ApiException Failure(string carrier, string operation, int status) =>
        new(502, $"{carrier} {operation}失败（HTTP {status}），请检查凭据、环境、Tracking 权限或调用额度。");

    public static CarrierTrackingResult Parse(string carrier, JsonNode json)
    {
        JsonNode? result;
        string? status;
        bool delivered;
        if (carrier == "fedex")
        {
            result = json["output"]?["completeTrackResults"]?.AsArray().FirstOrDefault()?["trackResults"]?.AsArray().FirstOrDefault();
            if (result?["error"] != null) throw new ApiException(502, "FedEx 未返回有效运单记录，请检查运单号和查询权限。");
            status = result?["latestStatusDetail"]?["description"]?.GetValue<string>();
            delivered = result?["latestStatusDetail"]?["code"]?.GetValue<string>() == "DL";
        }
        else
        {
            result = json is JsonArray array ? array.FirstOrDefault() : json;
            if (result?["error"] != null) throw new ApiException(502, "USPS 未返回有效运单记录，请检查运单号和查询权限。");
            status = result?["status"]?.GetValue<string>();
            // Do not infer delivery from prose such as "awaiting delivery".
            delivered = string.Equals(result?["statusCategory"]?.GetValue<string>(), "Delivered", StringComparison.OrdinalIgnoreCase);
        }
        if (string.IsNullOrWhiteSpace(status)) throw new ApiException(502, "承运商未返回有效运输状态，本地记录未更新。");
        return new(status.Length > 500 ? status[..500] : status, delivered);
    }
}
