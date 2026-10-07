using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Maui.Storage;
using Scanner.CheckListBoard.Models;

namespace Scanner.CheckListBoard.Services;

public sealed class AccountClient
{
    private readonly HttpClient client;
    public AccountClient() : this(new HttpClient { Timeout = TimeSpan.FromSeconds(20) }) { }
    public AccountClient(HttpClient client) => this.client = client;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public BoardSession? Session { get; private set; }
    public string ServerAddress
    {
        get => Preferences.Default.Get("checklist.server", "https://wms.ymforever.com/");
        set => Preferences.Default.Set("checklist.server", ValidateServer(value).AbsoluteUri);
    }
    public static Uri ValidateServer(string value)
    {
        if (!Uri.TryCreate(value.Trim().TrimEnd('/') + "/", UriKind.Absolute, out var uri)
            || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo)
            || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new ArgumentException("请输入有效的 HTTPS 服务地址。");
        return uri;
    }
    public async Task<BoardSession> LoginAsync(string username, string password)
    {
        Session = null;
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            throw new ArgumentException("请输入账号和密码。");
        using var response = await client.PostAsJsonAsync(new Uri(ValidateServer(ServerAddress), "api/account/login"), new { username = username.Trim(), password });
        string json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            string message = "登录失败，请检查账号、密码或稍后重试。";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message", out var item)) message = item.GetString() ?? message;
                else if (doc.RootElement.TryGetProperty("errors", out var errors))
                    message = string.Join(" ", errors.EnumerateObject().SelectMany(x => x.Value.EnumerateArray()).Select(x => x.GetString()));
            }
            catch (JsonException) { }
            throw new InvalidOperationException(message);
        }
        var session = JsonSerializer.Deserialize<BoardSession>(json, JsonOptions);
        if (session is null || string.IsNullOrWhiteSpace(session.Token) || session.ExpiresAt <= DateTimeOffset.UtcNow
            || session.Permissions is null || session.Role is not ("admin" or "user" or "viewer"))
            throw new InvalidOperationException("服务端返回的登录身份无效，请联系管理员。");
        Session = session;
        return session;
    }
    public async Task LogoutAsync()
    {
        var session = Session;
        Session = null;
        if (session is null) return;
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(ValidateServer(ServerAddress), "api/account/logout"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        using var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
    }
    public bool HasPermission(string permission) => Session is { } session
        && session.ExpiresAt > DateTimeOffset.UtcNow && session.Permissions.Contains(permission);

    public async Task<IReadOnlyList<WorkOrderRow>> DownloadWorkOrdersAsync()
    {
        var session = Session ?? throw new InvalidOperationException("请先登录。");
        if (session.UserId == 0) throw new InvalidOperationException("登录身份缺少用户ID，请部署新版服务端并重新登录。");
        var all = new List<WorkOrderRow>();
        ulong afterId = 0;
        while (true)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get,
                new Uri(ValidateServer(ServerAddress), $"api/checklist-work-orders?userId={session.UserId}&afterId={afterId}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
            using var response = await client.SendAsync(request);
            var rows = await ReadResponseAsync<List<WorkOrderRow>>(response);
            if (rows.Any(x => x.Id <= afterId || x.UserId != session.UserId))
                throw new InvalidOperationException("服务端工单身份或分页数据无效。");
            all.AddRange(rows);
            if (rows.Count < 200) break;
            afterId = rows.Max(x => x.Id);
        }
        for (int i = 0; i < all.Count; i++) all[i].Number = i + 1;
        return all;
    }
    public async Task<WorkOrderRow> UpdateWorkOrderAsync(WorkOrderRow row)
    {
        var session = Session ?? throw new InvalidOperationException("请先登录。");
        using var request = new HttpRequestMessage(HttpMethod.Put,
            new Uri(ValidateServer(ServerAddress), $"api/checklist-work-orders/{row.Id}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.Token);
        request.Content = JsonContent.Create(new { row.IsCompleted, row.Version });
        using var response = await client.SendAsync(request);
        return await ReadResponseAsync<WorkOrderRow>(response);
    }
    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response)
    {
        string json = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            string message = $"工单请求失败（HTTP {(int)response.StatusCode}）。";
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("message", out var item)) message = item.GetString() ?? message;
            }
            catch (JsonException) { }
            throw new InvalidOperationException(message);
        }
        return JsonSerializer.Deserialize<T>(json, JsonOptions) ?? throw new InvalidOperationException("服务端工单数据无效。");
    }
}

public sealed record BoardSession(string Username, string? DisplayName, string Role, string Token,
    DateTimeOffset ExpiresAt, string[] Permissions, string Message)
{
    public ulong UserId { get; init; }
}

public sealed record SavedAccount(string Username, string Password, string ServerAddress);

public static class AccountStorage
{
    private const string Key = "checklist.credentials.v1";
    public static async Task<SavedAccount?> ReadAsync()
    {
        string? json = await SecureStorage.Default.GetAsync(Key);
        return json is null ? null : JsonSerializer.Deserialize<SavedAccount>(json);
    }
    public static Task SaveAsync(SavedAccount account) => SecureStorage.Default.SetAsync(Key, JsonSerializer.Serialize(account));
    public static void Clear() => SecureStorage.Default.Remove(Key);
}
