using System.Net;
using System.Text.Json;
using Scanner.CheckListBoard.Services;

int count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); count++; }
var transport = new FakeHandler();
var accounts = new AccountClient(new HttpClient(transport));
accounts.ServerAddress = "https://example.test/";
var session = await accounts.LoginAsync(" alice ", " 密码 ");
Check(transport.Path == "/api/account/login", "Existing Web endpoint");
using (var json = JsonDocument.Parse(transport.Body!))
{
    Check(json.RootElement.GetProperty("username").GetString() == "alice", "Normalize username");
    Check(json.RootElement.GetProperty("password").GetString() == " 密码 ", "Preserve password");
}
Check(session.Role == "viewer" && accounts.HasPermission("checklist.read"), "Viewer read access");
Check(!accounts.HasPermission("checklist.write") && !accounts.HasPermission("system.configure"), "Viewer write/config denied");
await accounts.LogoutAsync();
Check(transport.Path == "/api/account/logout" && transport.Authorization == "Bearer session-token", "Revoke server session");
Check(accounts.Session is null, "Clear local session");
transport.Status = HttpStatusCode.Unauthorized;
try { await accounts.LoginAsync("alice", "wrong"); throw new Exception("Expected rejection"); }
catch (InvalidOperationException e) { Check(e.Message == "账户暂不可用。", "Display server rejection"); }
Check(accounts.Session is null, "Failed login never restores identity");
transport.Status = HttpStatusCode.OK;
transport.Role = "owner";
try { await accounts.LoginAsync("alice", "password"); throw new Exception("Expected role rejection"); }
catch (InvalidOperationException) { Check(accounts.Session is null, "Reject unknown role"); }
transport.Role = "user";
transport.Expired = true;
try { await accounts.LoginAsync("alice", "password"); throw new Exception("Expected expiry rejection"); }
catch (InvalidOperationException) { Check(accounts.Session is null, "Reject expired identity"); }
foreach (string invalid in new[] { "http://example.test", "https://alice:secret@example.test", "garbage", "https://example.test/?q=x" })
{
    try { AccountClient.ValidateServer(invalid); throw new Exception("Expected address rejection"); }
    catch (ArgumentException) { Check(true, "HTTPS address validation"); }
}
await AccountStorage.SaveAsync(new SavedAccount("alice", " 密码 ", accounts.ServerAddress));
Check(await AccountStorage.ReadAsync() is { Username: "alice", Password: " 密码 " }, "Saved credentials preserve original password");
AccountStorage.Clear();
Check(await AccountStorage.ReadAsync() is null, "Forget credentials");
var temporary = Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, ".artifacts", "checklist-workorders-tests-" + Guid.NewGuid().ToString("N")));
try
{
    var store = new WorkOrderStore(temporary.FullName, "https://example.test/|alice");
    var rows = await store.LoadAsync();
    Check(rows.Count == 10 && rows.Select(x => x.Number).Distinct().Count() == 10, "Ten numbered test records");
    Check(rows.All(x => x.DeviceId.StartsWith("TEST-DEV-")), "Clearly marked test device IDs");
    var row = rows[0];
    var changed = new List<string?>();
    row.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
    row.IsCompleted = true;
    Check(row.Completion == 1 && row.StatusText == "已完成", "Completed toggle maps to 1");
    Check(changed.Contains("IsCompleted") && changed.Contains("Completion") && changed.Contains("StatusText"), "Toggle refreshes bindings");
    Check(!(await store.LoadAsync())[0].IsCompleted, "Unsaved edits are not persisted");
    await store.SaveAsync(rows);
    Check((await store.LoadAsync())[0].IsCompleted, "Save persists completion");
    var other = new WorkOrderStore(temporary.FullName, "https://example.test/|bob");
    Check(!(await other.LoadAsync())[0].IsCompleted, "Account data isolated");
    row.IsCompleted = false;
    Check(row.Completion == 0 && row.StatusText == "未完成", "Incomplete toggle maps to 0");
    await store.SaveAsync(rows);
    Check(!(await store.LoadAsync())[0].IsCompleted, "Second save replaces previous state");
    Check(!Directory.EnumerateFiles(temporary.FullName, "*.tmp").Any(), "No unfinished save files");
}
finally
{
    foreach (var file in Directory.EnumerateFiles(temporary.FullName)) File.Delete(file);
    temporary.Delete();
}
transport.Expired = false;
transport.Role = "user";
transport.Status = HttpStatusCode.OK;
await accounts.LoginAsync("alice", "password");
var remoteOrders = await accounts.DownloadWorkOrdersAsync();
Check(remoteOrders.Count == 1 && remoteOrders[0].Id == 7 && remoteOrders[0].Number == 1, "Download remote work orders and number rows");
Check(transport.Authorization == "Bearer session-token", "Download carries session token");
remoteOrders[0].IsCompleted = true;
var savedOrder = await accounts.UpdateWorkOrderAsync(remoteOrders[0]);
Check(savedOrder.Version == 4 && transport.Path == "/api/checklist-work-orders/7", "Update returns refreshed server version");
using (var body = JsonDocument.Parse(transport.Body!))
{
    Check(body.RootElement.GetProperty("isCompleted").GetBoolean() && body.RootElement.GetProperty("version").GetUInt32() == 3, "Update includes completion and concurrency version");
    Check(!body.RootElement.TryGetProperty("userId", out _) && !body.RootElement.TryGetProperty("auditStatus", out _), "Tablet cannot reassign or audit work orders");
}
transport.WorkUser = 99;
try { await accounts.DownloadWorkOrdersAsync(); throw new Exception("Expected owner mismatch"); }
catch (InvalidOperationException) { Check(true, "Client rejects wrong-owner download data"); }
transport.WorkUser = 42;
transport.Status = HttpStatusCode.Conflict;
try { await accounts.UpdateWorkOrderAsync(remoteOrders[0]); throw new Exception("Expected conflict"); }
catch (InvalidOperationException) { Check(true, "Client surfaces save conflicts"); }
Console.WriteLine($"PASS: {count} CheckListBoard client checks (HTTP/secure-storage substitutes and real local work-order files).");

sealed class FakeHandler : HttpMessageHandler
{
    public string? Path, Body, Authorization;
    public HttpStatusCode Status = HttpStatusCode.OK;
    public string Role = "viewer";
    public bool Expired;
    public ulong WorkUser = 42;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Path = request.RequestUri!.AbsolutePath;
        Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        Authorization = request.Headers.Authorization?.ToString();
        string json = Status == HttpStatusCode.OK
            ? JsonSerializer.Serialize(new { username = "alice", displayName = "Alice", role = Role, token = "session-token", expiresAt = DateTimeOffset.UtcNow.AddHours(Expired ? -1 : 8), userId = 42, permissions = Role == "viewer" ? new[] { "checklist.read" } : new[] { "checklist.read", "checklist.write" }, message = "登录成功" })
            : "{\"message\":\"账户暂不可用。\"}";
        if (Status == HttpStatusCode.OK && Path == "/api/checklist-work-orders")
            json = JsonSerializer.Serialize(new[] { new { id = 7, userId = WorkUser, deviceId = "DEV-1", description = "Inspection", isCompleted = false, version = 3 } });
        if (Status == HttpStatusCode.OK && request.Method == HttpMethod.Put)
            json = JsonSerializer.Serialize(new { id = 7, userId = WorkUser, deviceId = "DEV-1", description = "Inspection", isCompleted = true, version = 4 });
        return new HttpResponseMessage(Status) { Content = new StringContent(json) };
    }
}
