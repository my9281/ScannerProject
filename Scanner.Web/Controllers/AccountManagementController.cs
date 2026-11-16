using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Filters;
using Scanner.Web.Models;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController, Route("api/account-management")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountManagementController(IAccountManagementService accounts) : ControllerBase
{
    private AccountResult Actor => (AccountResult)HttpContext.Items[typeof(AccountResult)]!;
    [HttpGet("users"), AccountPermission("system.configure")]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] ulong after = 0, [FromQuery] string search = "") => Ok(await accounts.List(Actor, after, search, ct));
    [HttpPost("users"), AccountPermission("system.configure"), RequestSizeLimit(128 * 1024)]
    public async Task<IActionResult> Create(CreateAccountRequest request, CancellationToken ct) => StatusCode(201, await accounts.Create(Actor, request, ct));
    [HttpPost("password"), AccountPermission("checklist.read"), RequestSizeLimit(128 * 1024)]
    public async Task<IActionResult> Change(ChangeAccountPasswordRequest request, CancellationToken ct)
    {
        await accounts.ChangePassword(Actor, request, ct);
        return Ok(new { message = "密码已修改，所有设备的旧登录已失效，请重新登录。" });
    }
    [HttpPost("users/{userId}/password"), AccountPermission("system.configure"), RequestSizeLimit(128 * 1024)]
    public async Task<IActionResult> Reset(ulong userId, ResetAccountPasswordRequest request, CancellationToken ct)
    {
        await accounts.ResetPassword(Actor, userId, request, ct);
        return Ok(new { message = "密码已重置，该用户所有设备需重新登录。" });
    }
}
