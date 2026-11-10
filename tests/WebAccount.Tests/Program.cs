using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scanner.Web.Controllers;
using Scanner.Web.Filters;
using Scanner.Web.Models;
using Scanner.Web.Services;

int checks = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception(name);
    checks++;
}
// Independent UTF-8 PBKDF2 vectors generated with Python hashlib, including significant trailing space.
byte[] salt = Enumerable.Range(0, 32).Select(x => (byte)x).ToArray();
string vector = "b70a614b3846ebcb59163e62d799ab9d4b156de815450e52ac2c93cdbeb6e09eb4520331152705dbe0cb65fad6931cb4aa205dbf4d63bd68461436dd143ddca2";
foreach (int length in new[] { 32, 64 })
{
    byte[] hash = Convert.FromHexString(vector[..(length * 2)]);
    Check(AccountSecurity.VerifyPassword("密码 example ", salt, hash, "PBKDF2-SHA256", 100000), $"{length}-byte compatibility");
    Check(!AccountSecurity.VerifyPassword("密码 example", salt, hash, "PBKDF2-SHA256", 100000), "Do not trim passwords");
    Check(!AccountSecurity.VerifyPassword("wrong", salt, hash, "PBKDF2-SHA256", 100000), "Wrong password");
    Check(!AccountSecurity.VerifyPassword("密码 example ", salt, hash, "SHA256", 100000), "Unsupported algorithm");
    Check(!AccountSecurity.VerifyPassword("密码 example ", salt, hash, "PBKDF2-SHA256", 0), "Zero iterations");
    Check(!AccountSecurity.VerifyPassword("密码 example ", salt, hash, "PBKDF2-SHA256", uint.MaxValue), "Unrepresentable iterations");
}
Check(!AccountSecurity.VerifyPassword("x", [], [1], "PBKDF2-SHA256", 1), "Empty salt");
DateTime now = DateTime.UtcNow;
Check(AccountSecurity.CanLogin("active", null, true, "admin", now), "Active account");
Check(!AccountSecurity.CanLogin("disabled", now.AddMinutes(-1), true, "admin", now), "Disabled never unlocks");
Check(!AccountSecurity.CanLogin("locked", null, true, "user", now), "Permanent lock");
Check(!AccountSecurity.CanLogin("locked", now.AddMinutes(1), true, "user", now), "Timed lock");
Check(AccountSecurity.CanLogin("locked", now, true, "user", now), "Timed lock expiry boundary");
Check(!AccountSecurity.CanLogin("active", now.AddMinutes(1), true, "user", now), "Lock date on active account");
Check(!AccountSecurity.CanLogin("active", null, false, "user", now), "Disabled domain");
Check(!AccountSecurity.CanLogin("active", null, true, "owner", now), "Unknown role");

FakeAccounts service = new();
AccountController controller = new(service) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
var rejected = await controller.Login(new LoginRequest { Username = "missing", Password = "wrong" }, default);
Check(rejected.Result is UnauthorizedObjectResult, "Login failure HTTP 401");
foreach (string role in new[] { "admin", "user", "viewer" })
{
    service.Current = new("alice", "Alice", new AccountDomain(1, "test"), role, "token", DateTimeOffset.UtcNow.AddHours(8));
    var accepted = await controller.Login(new LoginRequest { Username = "alice", Password = "password" }, default);
    Check(accepted.Result is OkObjectResult { Value: AccountResult a } && a.Role == role && a.Message.Contains("登录成功"), "Role login response");
    foreach (string permission in new[] { "checklist.read", "checklist.write", "system.configure" })
    {
        DefaultHttpContext http = new();
        using var provider = new ServiceCollection().AddSingleton<IAccountService>(service).BuildServiceProvider();
        http.RequestServices = provider;
        http.Request.Headers.Authorization = "Bearer token";
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
        await new AccountPermissionAttribute(permission).OnAuthorizationAsync(context);
        bool allowed = permission == "checklist.read" || permission == "checklist.write" && role != "viewer" || role == "admin";
        Check(allowed ? context.Result is null : context.Result is ObjectResult { StatusCode: 403 }, $"{role} / {permission}");
    }
}
controller.Request.Headers.Authorization = "Bearer malformed";
service.ThrowMalformed = true;
Check((await controller.Me(default)).Result is UnauthorizedObjectResult, "Malformed session HTTP 401");
Check(await controller.Logout(default) is OkObjectResult, "Malformed logout is idempotent");
service.ThrowMalformed = false;
service.Current = null;
Check((await controller.Me(default)).Result is UnauthorizedObjectResult, "Expired/revoked session HTTP 401");
Check(controller.Response.Headers.CacheControl == "no-store", "Identity responses cannot be cached");
var workService = new FakeWorkOrders();
var workController = new WorkOrdersController(workService) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
workController.HttpContext.Items[typeof(AccountResult)] = new AccountResult("alice", null, new AccountDomain(1, "test"), "user", "token", DateTimeOffset.UtcNow.AddHours(8)) { UserId = 42 };
Check(await workController.Download(99, 0) is ObjectResult { StatusCode: 403 } && workService.DownloadCalls == 0, "Spoofed download user ID rejected before data access");
Check(await workController.Download(null, 7) is OkObjectResult && workService.LastUser == 42 && workService.LastAfter == 7, "Download derives user ID from session and forwards cursor");
Check(await workController.Download(42) is OkObjectResult, "Matching tablet user ID accepted");
var update = new UpdateWorkOrderRequest { IsCompleted = true, Version = 3 };
Check(await workController.Update(8, update, default) is ConflictObjectResult, "Unavailable or conflicting work order returns 409");
Check(workService.LastUser == 42 && workService.LastId == 8 && workService.LastVersion == 3 && workService.LastCompleted, "Update scoped to session owner and version");
workService.Result = new WorkOrderResult(8, 42, "DEV-1", "Inspection", true, now, now, now, 4);
Check(await workController.Update(8, update, default) is OkObjectResult, "Successful update returns new version");
var errors = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
var missingFields = new UpdateWorkOrderRequest();
Check(!System.ComponentModel.DataAnnotations.Validator.TryValidateObject(missingFields, new(missingFields), errors, true), "Missing completion/version rejected");
var zeroVersion = new UpdateWorkOrderRequest { IsCompleted = false, Version = 0 };
Check(System.ComponentModel.DataAnnotations.Validator.TryValidateObject(zeroVersion, new(zeroVersion), [], true), "Incomplete and initial version zero accepted");
Check(workController.Response.Headers.CacheControl == "no-store", "Work-order responses cannot be cached");
var batchService = new WorkOrderBatchService(null!);
foreach (var invalidBatch in new[] {
    new WorkOrderBatchRequest(),
    new WorkOrderBatchRequest { Items = [new() { SourceKey = "bad", DeviceId = "SN", Description = "test" }] },
    new WorkOrderBatchRequest { Items = [new() { SourceKey = new string('a', 64), DeviceId = " ", Description = "test" }] },
    new WorkOrderBatchRequest { Items = Enumerable.Repeat(new WorkOrderUploadItem { SourceKey = new string('a',64), DeviceId = "SN", Description = "test" },501).ToList() }
})
{
    try { await batchService.UploadAsync(invalidBatch, default); throw new Exception("Invalid batch accepted"); }
    catch (ArgumentException) { Check(true, "Invalid batch rejected before database access"); }
}
Check(Attribute.IsDefined(typeof(WorkOrderUploadController), typeof(Scanner.Web.Filters.ApiKeyAttribute)), "Desktop batch API uses existing API key filter");
Check(WorkOrderUploadErrors.Describe(1146) is (503, var missingTable) && missingTable.Contains("006_work_order_sources.sql"), "Missing upload table gives migration instructions");
Check(WorkOrderUploadErrors.Describe(1054).Message.Contains("不会更新已有表"), "Old schema requires actual migration");
Check(WorkOrderUploadErrors.Describe(1142).Status == 503, "Database permission failure classified");
Check(WorkOrderUploadErrors.Describe(1213).Message.Contains("重试"), "Deadlock has retry guidance");
Check(WorkOrderUploadErrors.Describe(9999).Message.Contains("日志"), "Unexpected database error points to server log without leaking SQL");
Console.WriteLine($"PASS: {checks} account and work-order checks (no production database access).");

sealed class FakeAccounts : IAccountService
{
    public AccountResult? Current { get; set; }
    public bool ThrowMalformed { get; set; }
    public Task<AccountResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Current);
    public Task<AccountResult?> GetCurrentAsync(string token, CancellationToken cancellationToken = default) =>
        ThrowMalformed ? throw new FormatException() : Task.FromResult(Current);
    public Task LogoutAsync(string token, CancellationToken cancellationToken = default) =>
        ThrowMalformed ? throw new FormatException() : Task.CompletedTask;
    public Task<IReadOnlyList<AccountDomain>> GetDomainsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}

sealed class FakeWorkOrders : IWorkOrderService
{
    public int DownloadCalls;
    public ulong LastUser, LastAfter, LastId;
    public uint LastVersion;
    public bool LastCompleted;
    public WorkOrderResult? Result;
    public Task<IReadOnlyList<WorkOrderResult>> DownloadAsync(ulong userId, ulong afterId, CancellationToken cancellationToken)
    { DownloadCalls++; LastUser = userId; LastAfter = afterId; return Task.FromResult<IReadOnlyList<WorkOrderResult>>([]); }
    public Task<WorkOrderResult?> UpdateAsync(ulong userId, ulong id, bool completed, uint version, CancellationToken cancellationToken)
    { LastUser = userId; LastId = id; LastCompleted = completed; LastVersion = version; return Task.FromResult(Result); }
}
