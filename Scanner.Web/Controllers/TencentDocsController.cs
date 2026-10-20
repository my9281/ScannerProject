using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/tencent-docs")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TencentDocsController(TencentDocsService docs, BreezarySyncService sync, ILogger<TencentDocsController> logger) : ControllerBase
{
    private const string StateCookie = "__Host-ScannerTencentDocsState";

    [HttpGet("status")]
    [AccountPermission("system.configure")]
    public async Task<IActionResult> Status(CancellationToken ct) => Ok(await docs.StatusAsync(ct));

    [HttpPost("oauth/start")]
    [AccountPermission("system.configure")]
    public IActionResult Start()
    {
        var authorization = docs.BeginAuthorization();
        Response.Cookies.Append(StateCookie, authorization.State, new CookieOptions
        {
            HttpOnly = true, Secure = true, SameSite = SameSiteMode.Lax,
            Path = "/", MaxAge = TimeSpan.FromMinutes(10), IsEssential = true
        });
        return Ok(new { authorizationUrl = authorization.Url });
    }

    [HttpGet("oauth/callback")]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state,
        [FromQuery] string? error, CancellationToken ct)
    {
        Response.Headers["Referrer-Policy"] = "no-referrer";
        if (!docs.ConsumeState(state, Request.Cookies[StateCookie]))
            return BadRequest(new { message = "授权会话无效或已过期，请从管理员页面重新发起授权。" });
        Response.Cookies.Delete(StateCookie, new CookieOptions { Secure = true, HttpOnly = true, Path = "/", SameSite = SameSiteMode.Lax });
        if (!string.IsNullOrEmpty(error) || string.IsNullOrWhiteSpace(code))
            return Redirect("/tencent-docs.html?oauth=denied");
        try
        {
            await docs.CompleteAuthorizationAsync(code, ct);
            return Redirect("/tencent-docs.html?oauth=success");
        }
        catch (ApiException ex)
        {
            logger.LogWarning("Tencent Docs authorization failed: {Reason}", ex.Message);
            return Redirect("/tencent-docs.html?oauth=failed");
        }
    }

    [HttpGet("sheets")]
    [AccountPermission("system.configure")]
    public async Task<IActionResult> Sheets(CancellationToken ct) => Ok(await docs.GetSheetsAsync(ct));

    [HttpGet("records")]
    [AccountPermission("system.configure")]
    public async Task<IActionResult> Records([FromQuery] int offset = 0, [FromQuery] int limit = 100,
        CancellationToken ct = default) => Ok(await sync.ListAsync(offset, limit, ct));

    [HttpGet("remote-records")]
    [AccountPermission("system.configure")]
    public async Task<IActionResult> RemoteRecords([FromQuery] int offset = 0, [FromQuery] int limit = 100,
        CancellationToken ct = default) => Ok(await docs.GetRecordsAsync(offset, limit, ct));

    [HttpPost("sync/pull")]
    [AccountPermission("system.configure")]
    public async Task<IActionResult> Pull(CancellationToken ct) => Ok(await sync.PullAsync(ct));

    [HttpPost("sync/push")]
    [AccountPermission("system.configure")]
    public async Task<IActionResult> Push(CancellationToken ct) => Ok(await sync.PushAsync(ct));

    [HttpPatch("records/{recordId}")]
    [AccountPermission("system.configure")]
    [RequestSizeLimit(150000)]
    public async Task<IActionResult> Edit(string recordId, [FromBody] BreezaryEditRequest request, CancellationToken ct) =>
        Ok(await sync.EditAsync(recordId, request, ct));
}
