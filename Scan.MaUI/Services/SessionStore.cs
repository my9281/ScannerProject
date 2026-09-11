using Scanner.Models.MauiContracts;
using System.Text.Json;

namespace Scan.MaUI.Services;

public sealed class SessionStore
{
    private const string SessionKey = "scanner.auth.session";
    private const string UserKey = "scanner.auth.username";
    private const string PasswordKey = "scanner.auth.password";
    public AuthSession? Current { get; private set; }

    public async Task<AuthSession?> RestoreAsync()
    {
        try
        {
            string? json = await SecureStorage.Default.GetAsync(SessionKey);
            Current = string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<AuthSession>(json);
            if (Current?.IsValid() == true) return Current;
        }
        catch { }
        await ClearSessionAsync();
        return null;
    }

    public async Task SaveSessionAsync(AuthSession session)
    {
        Current = session;
        await SecureStorage.Default.SetAsync(SessionKey, JsonSerializer.Serialize(session));
    }

    public async Task SaveRememberedLoginAsync(string username, string password, bool remember)
    {
        if (!remember) { SecureStorage.Default.Remove(UserKey); SecureStorage.Default.Remove(PasswordKey); return; }
        await SecureStorage.Default.SetAsync(UserKey, username);
        await SecureStorage.Default.SetAsync(PasswordKey, password);
    }

    public async Task<(string User, string Password)> GetRememberedLoginAsync() =>
        (await SecureStorage.Default.GetAsync(UserKey) ?? string.Empty, await SecureStorage.Default.GetAsync(PasswordKey) ?? string.Empty);

    public Task ClearSessionAsync() { Current = null; SecureStorage.Default.Remove(SessionKey); return Task.CompletedTask; }
}
