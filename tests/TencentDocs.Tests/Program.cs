using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scanner.Web.Controllers;
using Scanner.Web.Extensions;
using Scanner.Web.Models;
using Scanner.Web.Services;

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; }
string root = Path.Combine(Path.GetTempPath(), "scanner-tencent-tests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
using var provider = new TencentHandler();
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    ContentRootPath = root, EnvironmentName = Environments.Development,
    WebRootPath = Path.GetFullPath("Scanner.Web/wwwroot")
});
builder.Logging.ClearProviders();
builder.WebHost.UseUrls("http://127.0.0.1:0");
builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
{
    ["TencentDocs:Enabled"] = "true", ["TencentDocs:ClientId"] = "client",
    ["TencentDocs:ClientSecret"] = "client-secret-test", ["TencentDocs:StoragePath"] = Path.Combine(root, "connection"),
    ["TencentDocs:RedirectUri"] = "https://scanner.example/api/tencent-docs/oauth/callback"
});
builder.Services.AddScannerWeb(builder.Configuration);
builder.Services.AddControllers().AddApplicationPart(typeof(TencentDocsController).Assembly);
builder.Services.AddSingleton<IAccountService, Accounts>();
builder.Services.AddSingleton<IHttpClientFactory>(new Factory(provider));
builder.Services.AddSingleton<IBreezaryStore, MemoryBreezaryStore>();
await using var app = builder.Build();
app.UseStaticFiles();
app.MapControllers();
await app.StartAsync();
using var transport = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
using var http = new HttpClient(transport) { BaseAddress = new Uri(app.Urls.Single()) };
async Task<HttpResponseMessage> Send(string path, string? role = "admin", HttpMethod? method = null, string? cookie = null)
{
    using var request = new HttpRequestMessage(method ?? HttpMethod.Get, path);
    if (role is not null) request.Headers.Authorization = new("Bearer", role);
    if (cookie is not null) request.Headers.Add("Cookie", cookie);
    return await http.SendAsync(request);
}
async Task<(string state, string cookie)> Begin()
{
    using var response = await Send("/api/tencent-docs/oauth/start", method: HttpMethod.Post);
    Check(response.IsSuccessStatusCode, "Admin can initiate OAuth");
    var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
    var url = new Uri(body["authorizationUrl"]!.ToString());
    var args = QueryHelpers.ParseQuery(url.Query);
    Check(url.Host == "docs.qq.com" && url.AbsolutePath == "/oauth/v2/authorize", "Official authorization endpoint");
    Check(args["client_id"] == "client" && args["redirect_uri"] == "https://scanner.example/api/tencent-docs/oauth/callback"
        && args["new_login"] == "1" && args["scope"] == "all", "Correct OAuth parameters");
    Check(!url.Query.Contains("client-secret-test"), "No client secret in browser URL");
    var cookie = response.Headers.GetValues("Set-Cookie").Single();
    Check(cookie.Contains("secure", StringComparison.OrdinalIgnoreCase) && cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase)
        && cookie.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase) && cookie.StartsWith("__Host-"), "Browser-bound secure OAuth cookie");
    return (args["state"].ToString(), cookie.Split(';')[0]);
}
try
{
    foreach (var route in new[] { "status", "sheets", "records", "oauth/start" })
    {
        foreach (var role in new string?[] { null, "viewer", "user" })
        {
            using var response = await Send("/api/tencent-docs/" + route, role, route == "oauth/start" ? HttpMethod.Post : null);
            Check(response.StatusCode == (role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden), "Protect " + route + " from " + role);
        }
    }
    Check(provider.Calls == 0, "Unauthorized requests never reach Tencent");
    using (var local = await Send("/api/tencent-docs/records"))
        Check(local.IsSuccessStatusCode && provider.Calls == 0, "Local reads work before Tencent authorization");
    foreach (var path in new[] { "sync/pull", "sync/push" })
        foreach (var role in new string?[] { null, "user", "viewer" })
        {
            using var denied = await Send("/api/tencent-docs/" + path, role, HttpMethod.Post);
            Check(denied.StatusCode == (role is null ? HttpStatusCode.Unauthorized : HttpStatusCode.Forbidden), "Protect sync " + path);
        }
    using (var response = await Send("/api/tencent-docs/status"))
    {
        var body = await response.Content.ReadAsStringAsync();
        Check(body.Contains("\"configured\":true") && body.Contains("\"connected\":false"), "Configured but not yet connected");
        Check(!body.Contains("client-secret-test") && !body.Contains("accessToken"), "Status never reveals secrets");
        Check(response.Headers.CacheControl?.NoStore == true, "Status is not cached");
    }
    using (var response = await Send("/api/tencent-docs/remote-records"))
        Check(response.StatusCode == HttpStatusCode.Conflict, "Read before authorization rejected");

    var first = await Begin();
    using (var response = await Send("/api/tencent-docs/oauth/callback?code=code-test&state=" + first.state, null))
        Check(response.StatusCode == HttpStatusCode.BadRequest, "State without browser cookie is rejected");
    using (var response = await Send("/api/tencent-docs/oauth/callback?code=code-test&state=wrong", null, cookie: first.cookie))
        Check(response.StatusCode == HttpStatusCode.BadRequest, "Wrong state rejected");
    Check(provider.TokenCalls == 0, "Invalid state cannot exchange code");
    using (var response = await Send("/api/tencent-docs/oauth/callback?error=access_denied&state=" + first.state, null, cookie: first.cookie))
        Check(response.Headers.Location?.ToString() == "/tencent-docs.html?oauth=denied", "User denial safely redirects");
    using (var response = await Send("/api/tencent-docs/oauth/callback?code=code-test&state=" + first.state, null, cookie: first.cookie))
        Check(response.StatusCode == HttpStatusCode.BadRequest, "Denied state cannot be replayed");

    var authorization = await Begin();
    using (var response = await Send("/api/tencent-docs/oauth/callback?code=code-test&state=" + authorization.state, null, cookie: authorization.cookie))
    {
        Check(response.Headers.Location?.ToString() == "/tencent-docs.html?oauth=success", "Successful code exchange redirects without tokens");
        Check(response.Headers.CacheControl?.NoStore == true, "Callback cannot be cached");
        Check(response.Headers.GetValues("Referrer-Policy").Single() == "no-referrer", "Callback does not leak query in referer");
    }
    using (var response = await Send("/api/tencent-docs/oauth/callback?code=code-test&state=" + authorization.state, null, cookie: authorization.cookie))
        Check(response.StatusCode == HttpStatusCode.BadRequest, "Successful state is single use");
    Check(provider.TokenCalls == 1, "Authorization code exchanged once");
    var storedPath = Path.Combine(root, "connection", "connection.protected");
    var stored = await File.ReadAllTextAsync(storedPath);
    Check(!stored.Contains("access-test") && !stored.Contains("refresh-test") && !stored.Contains("openid-test"), "Tokens encrypted at rest");
    var settings = app.Services.GetRequiredService<IOptions<TencentDocsOptions>>();
    var environment = app.Services.GetRequiredService<IWebHostEnvironment>();
    var restartedStore = new TencentDocsTokenStore(settings, environment);
    await using (var lease = await restartedStore.AcquireAsync(default))
    {
        var restored = await restartedStore.ReadAsync(default);
        Check(restored?.RefreshToken == "refresh-test", "Persistent keys decrypt connection after recreation");
    }
    using (var response = await Send("/api/tencent-docs/status"))
    {
        var body = await response.Content.ReadAsStringAsync();
        Check(body.Contains("\"connected\":true") && !body.Contains("refresh-test") && !body.Contains("access-test"), "Connected status excludes credentials");
    }
    var reads = await Task.WhenAll(Enumerable.Range(0, 3).Select(_ => Send("/api/tencent-docs/remote-records?offset=0&limit=1")));
    foreach (var response in reads)
    {
        using (response)
        {
            Check(response.IsSuccessStatusCode, "Read succeeds after automatic refresh");
            var page = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            Check(page["hasMore"]!.GetValue<bool>() && page["next"]!.GetValue<int>() == 1
                && page["records"]![0]!["recordID"]!.ToString() == "r1", "Preserve record page and next cursor");
        }
    }
    Check(provider.RefreshCalls == 1, "Concurrent requests refresh only once");
    Check(provider.ConverterCalls == 1, "Sharing ID converted to actual fileID");
    await using (var lease = await restartedStore.AcquireAsync(default))
        Check((await restartedStore.ReadAsync(default))?.RefreshToken == "refresh-test", "Refresh response without new refresh_token preserves original");
    using (var response = await Send("/api/tencent-docs/remote-records?offset=1&limit=1"))
    {
        var page = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Check(response.IsSuccessStatusCode && !page["hasMore"]!.GetValue<bool>() && page["records"]![0]!["recordID"]!.ToString() == "r2", "Read final page");
    }
    using (var response = await Send("/api/tencent-docs/sheets"))
        Check(response.IsSuccessStatusCode && (await response.Content.ReadAsStringAsync()).Contains("t00i2h"), "Read worksheet list");
    var before = provider.Calls;
    foreach (var query in new[] { "offset=-1", "limit=0", "limit=101" })
    {
        using var response = await Send("/api/tencent-docs/remote-records?" + query);
        Check(response.StatusCode == HttpStatusCode.BadRequest, "Validate paging bounds");
    }
    Check(provider.Calls == before, "Invalid paging never reaches Tencent");
    provider.Mode = "provider-error";
    using (var response = await Send("/api/tencent-docs/remote-records"))
    {
        var body = await response.Content.ReadAsStringAsync();
        Check(response.StatusCode == HttpStatusCode.BadGateway && body.Contains("37019"), "Surface provider error code");
        Check(!body.Contains("client-secret-test") && !body.Contains("access-new-test"), "Never echo provider error secrets");
    }
    provider.Mode = "empty";
    using (var response = await Send("/api/tencent-docs/remote-records"))
    {
        var page = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Check(response.IsSuccessStatusCode && ((JsonArray)page["records"]!).Count == 0, "Valid empty table succeeds");
    }
    provider.Mode = "malformed";
    using (var response = await Send("/api/tencent-docs/remote-records"))
        Check(response.StatusCode == HttpStatusCode.BadGateway, "Missing records is not a successful empty table");
    provider.Mode = "bad-page";
    using (var response = await Send("/api/tencent-docs/remote-records"))
        Check(response.StatusCode == HttpStatusCode.BadGateway, "Non-progressing pagination rejected");
    provider.Mode = "token-fail";
    var failed = await Begin();
    using (var response = await Send("/api/tencent-docs/oauth/callback?code=code-test&state=" + failed.state, null, cookie: failed.cookie))
        Check(response.Headers.Location?.ToString() == "/tencent-docs.html?oauth=failed", "Token failure redirects safely");
    Check(await File.ReadAllTextAsync(storedPath) != "" && !provider.SentWrite, "Existing connection preserved; no business write requests");
    var disabled = new TencentDocsService(Options.Create(new TencentDocsOptions()), new Factory(provider), restartedStore);
    Check(!(await disabled.StatusAsync(default)).Configured, "Feature disabled by default");
    try { disabled.BeginAuthorization(); throw new Exception("Disabled authorization accepted"); }
    catch (ApiException ex) { Check(ex.StatusCode == 503, "Disabled start gives configuration instructions"); }
    Check(!new TencentDocsOptions { Enabled = true, ClientId = "x", ClientSecret = "y", RedirectUri = "http://scanner.example/api/tencent-docs/oauth/callback" }.IsConfigured, "Reject non-HTTPS callback");
    try
    {
        TencentDocsOptions.ResolveStoragePath(new() { StoragePath = Path.Combine(environment.WebRootPath, "private") }, environment);
        throw new Exception("Public token storage accepted");
    }
    catch (InvalidOperationException) { checks++; }
    provider.Mode = "success";
    var directOptions = new TencentDocsOptions
    {
        Enabled = true, AuthMode = "DirectToken", ClientId = "client", AccessToken = "access-new-test",
        OpenId = "openid-test", AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
    };
    var direct = new TencentDocsService(Options.Create(directOptions), new Factory(provider), restartedStore);
    var tokenCallsBeforeDirect = provider.TokenCalls + provider.RefreshCalls;
    Check(directOptions.IsConfigured, "Direct credentials do not require client secret or redirect");
    var directStatus = await direct.StatusAsync(default);
    Check(directStatus.Connected && directStatus.AuthMode == "DirectToken", "Direct token connection status");
    Check(!System.Text.Json.JsonSerializer.Serialize(directStatus).Contains("access-new-test"), "Direct status excludes access token");
    Check((await direct.GetRecordsAsync(0, 100, default)).Records.Count == 1, "Direct credentials read real API request shape");
    Check(provider.TokenCalls + provider.RefreshCalls == tokenCallsBeforeDirect, "Direct mode never exchanges or refreshes tokens");
    try { direct.BeginAuthorization(); throw new Exception("Direct mode started OAuth"); }
    catch (ApiException ex) { Check(ex.StatusCode == 409, "Direct mode blocks OAuth start"); }
    directOptions.AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
    Check(!(await direct.StatusAsync(default)).Connected, "Expired direct token is disconnected");
    try { await direct.GetRecordsAsync(0, 100, default); throw new Exception("Expired direct token accepted"); }
    catch (ApiException ex) { Check(ex.StatusCode == 409, "Expired direct token asks for manual update"); }
    checks += await BreezarySyncChecks.RunAsync();
    provider.AllowWrite = true;
    provider.Mode = "success";
    using (var pull = await Send("/api/tencent-docs/sync/pull", method: HttpMethod.Post))
        Check(pull.IsSuccessStatusCode, "Pull controller persists remote records");
    var beforeLocalRead = provider.Calls;
    using (var local = await Send("/api/tencent-docs/records"))
    {
        var page = JsonNode.Parse(await local.Content.ReadAsStringAsync())!;
        Check(page["total"]!.GetValue<int>() == 2 && provider.Calls == beforeLocalRead, "Local controller does not call Tencent");
        using var edit = new HttpRequestMessage(HttpMethod.Patch, "/api/tencent-docs/records/r1");
        edit.Headers.Authorization = new("Bearer", "admin");
        edit.Content = JsonContent.Create(new { version = page["records"]![0]!["version"]!.GetValue<long>(), values = new JsonObject { ["工单号"] = "WO-EDIT" } });
        using var edited = await http.SendAsync(edit);
        Check(edited.IsSuccessStatusCode && provider.Calls == beforeLocalRead, "Edit controller saves pending change locally");
    }
    using (var pushed = await Send("/api/tencent-docs/sync/push", method: HttpMethod.Post))
    {
        var result = JsonNode.Parse(await pushed.Content.ReadAsStringAsync())!;
        Check(pushed.IsSuccessStatusCode && result["pushed"]!.GetValue<int>() == 1 && provider.SentWrite, "Push controller sends updateRecords to Tencent");
    }
    Console.WriteLine($"Passed {checks} Tencent Docs checks; no production accounts or Tencent network calls.");
    if (args.Contains("--preview"))
    {
        Console.WriteLine("Preview at " + http.BaseAddress + "tencent-docs.html (fake account token: admin)");
        provider.Mode = "success";
        await app.WaitForShutdownAsync();
    }
}
finally
{
    await app.StopAsync();
    // Keep the isolated test artifacts for inspection; no customer data or real credentials are present.
}

sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}
sealed class Accounts : IAccountService
{
    public Task<AccountResult?> GetCurrentAsync(string token, CancellationToken ct = default) => Task.FromResult<AccountResult?>(
        token is "admin" or "viewer" or "user" ? new("test", null, new AccountDomain(1, "test"), token, token, DateTimeOffset.UtcNow.AddHours(1)) : null);
    public Task<IReadOnlyList<AccountDomain>> GetDomainsAsync(CancellationToken ct = default) => throw new NotSupportedException();
    public Task<AccountResult?> LoginAsync(LoginRequest request, CancellationToken ct = default) =>
        request.Password == "preview" ? GetCurrentAsync(request.Username, ct) : Task.FromResult<AccountResult?>(null);
    public Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken ct = default) => throw new NotSupportedException();
    public Task LogoutAsync(string token, CancellationToken ct = default) => Task.CompletedTask;
}
sealed class TencentHandler : HttpMessageHandler
{
    public int Calls, TokenCalls, RefreshCalls, ConverterCalls;
    public bool SentWrite;
    public bool AllowWrite;
    public string Mode = "success";
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Interlocked.Increment(ref Calls);
        var uri = request.RequestUri!;
        if (uri.Scheme != "https" || uri.Host != "docs.qq.com") throw new Exception("Unexpected provider destination");
        var query = QueryHelpers.ParseQuery(uri.Query);
        if (uri.AbsolutePath == "/oauth/v2/token")
        {
            if (request.Method != HttpMethod.Get || query["client_id"] != "client" || query["client_secret"] != "client-secret-test")
                throw new Exception("Wrong token exchange credentials or method");
            if (Mode == "token-fail") return Reply("""{"error":"invalid_grant","error_description":"client-secret-test"}""");
            if (query["grant_type"] == "authorization_code")
            {
                Interlocked.Increment(ref TokenCalls);
                if (query["code"] != "code-test" || query["redirect_uri"] != "https://scanner.example/api/tencent-docs/oauth/callback") throw new Exception("Wrong code exchange parameters");
                return Reply("""{"access_token":"access-test","refresh_token":"refresh-test","user_id":"openid-test","expires_in":1}""");
            }
            Interlocked.Increment(ref RefreshCalls);
            if (query["grant_type"] != "refresh_token" || query["refresh_token"] != "refresh-test") throw new Exception("Wrong refresh request");
            await Task.Delay(20, ct);
            return Reply("""{"access_token":"access-new-test","user_id":"openid-test","expires_in":3600}""");
        }
        if (request.Headers.GetValues("Access-Token").Single() != "access-new-test"
            || request.Headers.GetValues("Client-Id").Single() != "client"
            || request.Headers.GetValues("Open-Id").Single() != "openid-test") throw new Exception("Missing Open API authentication triple");
        if (uri.AbsolutePath == "/openapi/drive/v2/util/converter")
        {
            Interlocked.Increment(ref ConverterCalls);
            if (query["type"] != "2" || query["value"] != "DYk5kdUx1aGJUQXdp") throw new Exception("Wrong document ID conversion");
            return Reply("""{"ret":0,"data":{"fileID":"300$REAL"}}""");
        }
        if (uri.AbsolutePath.EndsWith("/sheets") && request.Method == HttpMethod.Get)
            return Reply("""{"ret":0,"data":{"sheets":[{"sheetID":"t00i2h"}]}}""");
        if (!Uri.UnescapeDataString(uri.AbsolutePath).EndsWith("/300$REAL/sheets/t00i2h")) throw new Exception("Wrong target document");
        var body = JsonNode.Parse(await request.Content!.ReadAsStringAsync(ct))!;
        if (body["updateRecords"] is JsonObject update && AllowWrite)
        {
            SentWrite = true;
            var changed = update["records"]![0]!;
            if (changed["recordID"]!.ToString() != "r1" || changed["values"]!.AsObject().Count != 1)
                throw new Exception("Push must send only edited fields for correct record ID");
            return Reply(new JsonObject { ["ret"] = 0, ["data"] = new JsonObject { ["updateRecords"] = new JsonObject { ["records"] = new JsonArray(changed.DeepClone()) } } }.ToJsonString());
        }
        if (body["getRecords"] is null) { SentWrite = true; throw new Exception("Unexpected business write"); }
        if (Mode == "provider-error") return Reply("""{"ret":37019,"msg":"secret access-new-test client-secret-test"}""");
        if (Mode == "malformed") return Reply("""{"ret":0,"data":{"getRecords":{}}}""");
        if (Mode == "bad-page") return Reply("""{"ret":0,"data":{"getRecords":{"records":[{}],"hasMore":true,"next":0}}}""");
        if (Mode == "empty") return Reply("""{"ret":0,"data":{"getRecords":{"records":[],"hasMore":false,"next":0,"total":0}}}""");
        return Reply(body["getRecords"]!["offset"]!.GetValue<int>() == 0
            ? """{"ret":0,"data":{"getRecords":{"records":[{"recordID":"r1","values":{"工单号":[{"text":"WO-001"}],"渠道":[{"label":"UPS"}]}}],"hasMore":true,"next":1,"total":2}}}"""
            : """{"ret":0,"data":{"getRecords":{"records":[{"recordID":"r2","values":{"运单号":"TRACK-002"}}],"hasMore":false,"next":0,"total":2}}}""");
    }
    private static HttpResponseMessage Reply(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
}
