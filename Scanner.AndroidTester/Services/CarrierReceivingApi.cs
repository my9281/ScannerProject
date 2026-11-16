using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Scanner.Server.Model;

namespace Scanner.AndroidTester.Services;

public sealed record ReceivingLogin(string Token, DateTimeOffset ExpiresAt, CarrierReceiptContext Context);

public sealed class CarrierReceivingApi(HttpClient client)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static async Task<T> Read<T>(HttpResponseMessage response, CancellationToken ct)
    {
        string body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            string message = "请求失败（HTTP " + (int)response.StatusCode + "），未确认记录已保留。";
            try { using var json = JsonDocument.Parse(body); message = json.RootElement.GetProperty("message").GetString() ?? message; } catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException) { }
            throw new InvalidOperationException(message);
        }
        return JsonSerializer.Deserialize<T>(body, Json) ?? throw new InvalidDataException("服务器响应为空，记录已保留。");
    }
    public async Task<ReceivingLogin> Login(string username, string password, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("api/account/login", new { username, password }, ct);
        var login = await Read<LoginResult>(response, ct);
        var context = await Context(login.Token, ct);
        return new(login.Token, login.ExpiresAt, context);
    }
    private sealed record LoginResult(string Token, DateTimeOffset ExpiresAt);
    public async Task<CarrierReceiptContext> Context(string token, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "api/carrier-receipts/context");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, ct);
        return (await Read<CarrierReceiptContext>(response, ct)) with { Server = client.BaseAddress?.AbsoluteUri ?? throw new InvalidOperationException("未配置服务器地址。") };
    }
    public async Task<CarrierReceiptAcknowledgement> Upload(string token, CarrierReceiptBatch batch, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/carrier-receipts") { Content = JsonContent.Create(batch) };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await client.SendAsync(request, ct);
        return await Read<CarrierReceiptAcknowledgement>(response, ct);
    }
}
