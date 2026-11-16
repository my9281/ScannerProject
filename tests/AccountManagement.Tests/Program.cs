using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using Scanner.Server.DAL;
using Scanner.Web.Controllers;
using Scanner.Web.Filters;
using Scanner.Web.Models;
using Scanner.Web.Services;
using System.ComponentModel.DataAnnotations;

int checks = 0;
void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
var admin = new AccountResult("admin", "管理员", new AccountDomain(7, "warehouse"), "admin", "dGVzdA==", DateTimeOffset.UtcNow.AddHours(1)) { UserId = 42 };
foreach (string role in new[] { "viewer", "user", "admin" })
{
    foreach (var method in typeof(AccountManagementController).GetMethods().Where(m => m.DeclaringType == typeof(AccountManagementController)))
    {
        var permission = method.GetCustomAttributes(typeof(AccountPermissionAttribute), false).Cast<AccountPermissionAttribute>().Single();
        using var provider = new ServiceCollection().AddSingleton<IAccountService>(new FakeAccounts(admin with { Role = role })).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = provider }; http.Request.Headers.Authorization = "Bearer token";
        var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
        await permission.OnAuthorizationAsync(context);
        Check((context.Result is null) == (method.Name == "Change" || role == "admin"), role + " " + method.Name);
    }
}
using (var provider = new ServiceCollection().AddSingleton<IAccountService>(new FakeAccounts(null)).BuildServiceProvider())
{
    var http = new DefaultHttpContext { RequestServices = provider };
    var context = new AuthorizationFilterContext(new ActionContext(http, new RouteData(), new ActionDescriptor()), []);
    await new AccountPermissionAttribute("system.configure").OnAuthorizationAsync(context);
    Check(context.Result is UnauthorizedObjectResult, "anonymous administration rejected");
}
var connections = new NoDatabase();
var service = new AccountManagementService(connections, NullLogger<AccountManagementService>.Instance);
async Task Reject(Func<Task> action, int? status = null)
{
    try { await action(); }
    catch (ArgumentException) when (status is null) { checks++; return; }
    catch (ApiException ex) when (status == ex.StatusCode) { checks++; return; }
    throw new Exception("Invalid management request was not rejected");
}
await Reject(() => service.Create(admin with { Role = "user" }, new(), default), 403);
await Reject(() => service.Create(admin, new() { Username = "new", Role = "owner", Password = "secret!", ConfirmPassword = "secret!" }, default));
await Reject(() => service.Create(admin, new() { Username = "new\nname", Password = "secret!", ConfirmPassword = "secret!" }, default));
await Reject(() => service.Create(admin, new() { Username = "new", Password = "secret!", ConfirmPassword = "different" }, default));
await Reject(() => service.ResetPassword(admin, admin.UserId, new() { NewPassword = "secret!", ConfirmPassword = "secret!" }, default));
await Reject(() => service.ResetPassword(admin with { Role = "viewer" }, 45, new(), default), 403);
await Reject(() => service.ChangePassword(admin, new() { CurrentPassword = "", NewPassword = "secret!", ConfirmPassword = "secret!" }, default));
foreach (var password in new[] { "short", new string('x', 4097), "      " })
    await Reject(() => service.ChangePassword(admin, new() { CurrentPassword = "old", NewPassword = password, ConfirmPassword = password }, default));
Check(connections.Calls == 0, "invalid requests never open a database connection");
AccountManagementService.ValidatePassword(" password ", " password "); checks++;
var create = new CreateAccountRequest { Username = "alice", DisplayName = "收货员", Role = "user", Password = "secret!", ConfirmPassword = "secret!" };
Check(Validator.TryValidateObject(create, new ValidationContext(create), [], true), "valid create model");
var invalid = new ChangeAccountPasswordRequest { CurrentPassword = "old", NewPassword = "secret!", ConfirmPassword = "other" };
Check(!Validator.TryValidateObject(invalid, new ValidationContext(invalid), [], true), "confirmation model validation");

var manager = new RecordingManager();
var controller = new AccountManagementController(manager) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
controller.HttpContext.Items[typeof(AccountResult)] = admin;
var created = await controller.Create(create, default);
Check(created is ObjectResult { StatusCode: 201, Value: ManagedAccount { Id: "45", Username: "alice" } }, "create has safe user payload and 201 status");
Check(manager.Actor?.Domain.Id == 7 && manager.Actor.UserId == 42, "uses authenticated actor and domain");
Check(created is ObjectResult { Value: not AccountResult }, "does not issue a new user's session");
Check((await controller.Change(new() { CurrentPassword = "old", NewPassword = "secret!", ConfirmPassword = "secret!" }, default)) is OkObjectResult && manager.Changed, "self password route");
Check((await controller.Reset(45, new() { NewPassword = "secret!", ConfirmPassword = "secret!" }, default)) is OkObjectResult && manager.Target == 45, "admin reset route");
Check(typeof(CreateAccountRequest).GetProperty("DomainId") is null, "client cannot choose another domain");
Console.WriteLine($"Passed {checks} account management checks. No production database accessed.");

sealed class NoDatabase : IMySqlConnectionFactory
{
    public int Calls;
    public Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default) { Calls++; throw new Exception("Database should not be opened"); }
}
sealed class RecordingManager : IAccountManagementService
{
    public AccountResult? Actor; public bool Changed; public ulong Target;
    public Task<IReadOnlyList<ManagedAccount>> List(AccountResult actor, ulong after, string search, CancellationToken ct) => Task.FromResult<IReadOnlyList<ManagedAccount>>([]);
    public Task<ManagedAccount> Create(AccountResult actor, CreateAccountRequest request, CancellationToken ct) { Actor = actor; return Task.FromResult(new ManagedAccount("45", request.Username, request.DisplayName, request.Role, "active")); }
    public Task ChangePassword(AccountResult actor, ChangeAccountPasswordRequest request, CancellationToken ct) { Changed = true; return Task.CompletedTask; }
    public Task ResetPassword(AccountResult actor, ulong userId, ResetAccountPasswordRequest request, CancellationToken ct) { Target = userId; return Task.CompletedTask; }
}
sealed class FakeAccounts(AccountResult? account) : IAccountService
{
    public Task<AccountResult?> GetCurrentAsync(string token, CancellationToken cancellationToken = default) => Task.FromResult(account);
    public Task<IReadOnlyList<AccountDomain>> GetDomainsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AccountResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task LogoutAsync(string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
