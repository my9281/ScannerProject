using MaUIScanner.Models;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace MaUIScanner.Services;

public sealed class AuthService
{
    private readonly HttpClient _client = new() { BaseAddress = new Uri("https://repair-rms.vercel.app"), Timeout = TimeSpan.FromSeconds(20) };

    public async Task<AuthSession> LoginAsync(string username, string password)
    {
        if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("用户名不能为空。");
        if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("密码不能为空。");
        using HttpResponseMessage response = await _client.PostAsJsonAsync("/api/v1/auth/login", new LoginRequest { Username = username.Trim(), Password = password });
        string json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            string message = TryGetError(json);
            if (response.StatusCode == HttpStatusCode.Unauthorized) throw new InvalidOperationException(string.IsNullOrWhiteSpace(message) ? "用户名或密码错误。" : message);
            throw new InvalidOperationException($"登录失败（HTTP {(int)response.StatusCode}）：{message}");
        }
        LoginResponse? result = JsonSerializer.Deserialize<LoginResponse>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (result == null || string.IsNullOrWhiteSpace(result.Token)) throw new InvalidOperationException("服务器没有返回有效 Token。");
        DateTime expiresAt = DateTime.TryParse(result.ExpiresAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed) ? parsed.ToUniversalTime() : DateTime.UtcNow.AddHours(12);
        return new AuthSession { Token = result.Token, Operator = result.Operator, Role = result.Role, ExpiresAt = expiresAt };
    }

    private static string TryGetError(string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.TryGetProperty("message", out JsonElement message)) return message.GetString() ?? string.Empty;
            if (document.RootElement.TryGetProperty("error", out JsonElement error)) return error.GetString() ?? string.Empty;
        }
        catch (JsonException) { }
        return json.Length > 300 ? json[..300] : json;
    }
}
