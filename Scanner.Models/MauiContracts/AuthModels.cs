using System.Text.Json.Serialization;

namespace Scanner.Models.MauiContracts;

public sealed class AuthSession
{
    public string Token { get; set; } = string.Empty;
    public string Operator { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsLocalMode { get; set; }
    public bool IsValid() => IsLocalMode || !string.IsNullOrWhiteSpace(Token) && ExpiresAt.ToUniversalTime() > DateTime.UtcNow.AddMinutes(1);
}

public sealed class LoginRequest
{
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;
}

public sealed class LoginResponse
{
    [JsonPropertyName("token")] public string Token { get; set; } = string.Empty;
    [JsonPropertyName("operator")] public string Operator { get; set; } = string.Empty;
    [JsonPropertyName("role")] public string Role { get; set; } = string.Empty;
    [JsonPropertyName("expires_at")] public string ExpiresAt { get; set; } = string.Empty;
}
