using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Routing;
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
var domain = await controller.CreateDomain(new("第二仓"), default);
Check(domain is ObjectResult { StatusCode: 201, Value: AccountDomain { DomainName: "第二仓" } }, "admin creates a domain");
Check(await controller.AssignDomain(45, new(8), default) is NoContentResult && manager.AssignedDomain == 8,
    "admin assigns an existing user to another domain");
var assignmentRoute = typeof(AccountManagementController).GetMethod(nameof(AccountManagementController.AssignDomain))!
    .GetCustomAttributes(false).OfType<IActionHttpMethodProvider>().Single();
Check(assignmentRoute.HttpMethods.Contains("POST") && assignmentRoute.HttpMethods.Contains("PUT"),
    "domain assignment accepts dashboard POST and legacy PUT");
await Reject(() => service.CreateDomain(admin with { Role = "viewer" }, new("第二仓"), default), 403);
await Reject(() => service.CreateDomain(admin, new("  "), default));
await Reject(() => service.AssignDomain(admin, 45, new(0), default));
var session = new WebSessionAccounts(admin);
var loginHttp = new DefaultHttpContext();
loginHttp.Request.Scheme = "https";
loginHttp.Request.Headers["Sec-Fetch-Site"] = "same-origin";
var web = new AccountController(session) { ControllerContext = new ControllerContext { HttpContext = loginHttp } };
var loginResult = (OkObjectResult)await web.WebLogin(new() { Username = "admin", Password = "password" }, default);
var cookie = Microsoft.Net.Http.Headers.SetCookieHeaderValue.Parse(loginHttp.Response.Headers.SetCookie.ToString());
Check(cookie.HttpOnly && cookie.Secure && cookie.SameSite == Microsoft.Net.Http.Headers.SameSiteMode.Lax &&
    cookie.Expires > DateTimeOffset.UtcNow.AddDays(7).AddMinutes(-1), "web login sets secure seven-day session cookie");
Check(loginResult.Value?.GetType().GetProperty("Token") is null, "web login JSON contains no session key");
var meHttp = new DefaultHttpContext();
meHttp.Request.Scheme = "https";
meHttp.Request.Method = "GET";
meHttp.Request.Headers.Cookie = $"{cookie.Name}={cookie.Value}";
web.ControllerContext = new ControllerContext { HttpContext = meHttp };
var profile = (OkObjectResult)await web.Me();
Check(session.Renewed == 1 && meHttp.Response.Headers.SetCookie.Count == 1 &&
    profile.Value?.GetType().GetProperty("Token") is null, "cookie authentication renews session and hides key");
using (var provider = new ServiceCollection().AddSingleton<IAccountService>(session).BuildServiceProvider())
{
    var unsafeHttp = new DefaultHttpContext { RequestServices = provider };
    unsafeHttp.Request.Method = "POST";
    unsafeHttp.Request.Headers.Cookie = meHttp.Request.Headers.Cookie;
    unsafeHttp.Request.Headers["Sec-Fetch-Site"] = "same-site";
    var filter = new AuthorizationFilterContext(new ActionContext(unsafeHttp, new RouteData(), new ActionDescriptor()), []);
    await new AccountPermissionAttribute("system.configure").OnAuthorizationAsync(filter);
    Check(filter.Result is UnauthorizedObjectResult && session.Renewed == 1, "cross-origin writes cannot use session cookie");
}
var logoutHttp = new DefaultHttpContext();
logoutHttp.Request.Scheme = "https";
logoutHttp.Request.Headers.Cookie = meHttp.Request.Headers.Cookie;
logoutHttp.Request.Headers["Sec-Fetch-Site"] = "same-origin";
web.ControllerContext = new ControllerContext { HttpContext = logoutHttp };
Check(await web.Logout(default) is OkObjectResult && session.Revoked &&
    Microsoft.Net.Http.Headers.SetCookieHeaderValue.Parse(logoutHttp.Response.Headers.SetCookie.ToString()).Expires < DateTimeOffset.UtcNow,
    "web logout revokes server session and expires browser cookie");
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
    public int AssignedDomain;
    public Task<IReadOnlyList<AccountDomain>> ListDomains(AccountResult actor, CancellationToken ct) => Task.FromResult<IReadOnlyList<AccountDomain>>([]);
    public Task<AccountDomain> CreateDomain(AccountResult actor, CreateDomainRequest request, CancellationToken ct) =>
        Task.FromResult(new AccountDomain(8, request.DomainName));
    public Task AssignDomain(AccountResult actor, ulong userId, AssignDomainRequest request, CancellationToken ct)
    {
        Target = userId;
        AssignedDomain = request.DomainId;
        return Task.CompletedTask;
    }
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

sealed class WebSessionAccounts(AccountResult account) : IAccountService
{
    public int Renewed;
    public bool Revoked;
    public Task<AccountResult?> LoginWebAsync(LoginRequest request, CancellationToken cancellationToken = default) =>
        Task.FromResult<AccountResult?>(account with { ExpiresAt = DateTimeOffset.UtcNow.AddDays(7) });
    public Task<AccountResult?> RenewWebSessionAsync(string token, CancellationToken cancellationToken = default)
    {
        Renewed++;
        return token == account.Token && !Revoked
            ? LoginWebAsync(new(), cancellationToken) : Task.FromResult<AccountResult?>(null);
    }
    public Task LogoutAsync(string token, CancellationToken cancellationToken = default)
    {
        Revoked = token == account.Token;
        return Task.CompletedTask;
    }
    public Task<AccountResult?> GetCurrentAsync(string token, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<IReadOnlyList<AccountDomain>> GetDomainsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AccountResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    public Task<AccountResult?> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
