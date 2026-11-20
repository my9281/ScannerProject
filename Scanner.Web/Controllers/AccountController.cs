using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Models;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/account")]
public sealed class AccountController(IAccountService accounts) : ControllerBase
{
    [HttpGet("/account/login")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult LoginPage() => Redirect("/login.html");

    [HttpGet("/account/register")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult RegisterPage() => Redirect("/register.html");

    [HttpGet("domains")]
    [ProducesResponseType<IReadOnlyList<AccountDomain>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AccountDomain>>> GetDomains(CancellationToken cancellationToken)
        => Ok(await accounts.GetDomainsAsync(cancellationToken));

    [HttpPost("register")]
    [ProducesResponseType<AccountResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AccountResult>> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        try
        {
            AccountResult result = await accounts.RegisterAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (AccountAlreadyExistsException exception)
        {
            return Conflict(new { message = exception.Message });
        }
    }

    [HttpPost("login")]
    [ProducesResponseType<AccountResult>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AccountResult>> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        AccountResult? result = await accounts.LoginAsync(request, cancellationToken);
        return result is null
            ? Unauthorized(new { message = "登录失败：用户名或密码错误，或账户暂不可用，请稍后重试或联系管理员。" })
            : Ok(result);
    }

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        Response.Headers.CacheControl = "no-store";
        AccountResult? result = await WebAccountSession.ResolveAsync(HttpContext, accounts);
        if (result is null) return Unauthorized(new { message = "登录已失效，请重新登录。" });
        return WebAccountSession.BearerToken(Request) is null ? Ok(Profile(result)) : Ok(result);
    }

    [HttpPost("web-login")]
    public async Task<IActionResult> WebLogin(LoginRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        if (!WebAccountSession.IsSameOrigin(Request))
            return StatusCode(403, new { message = "请从本站登录。" });
        AccountResult? result = await accounts.LoginWebAsync(request, cancellationToken);
        if (result is null)
            return Unauthorized(new { message = "登录失败：用户名或密码错误，或账户暂不可用，请稍后重试或联系管理员。" });
        WebAccountSession.Write(HttpContext, result);
        return Ok(Profile(result));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        string? bearer = WebAccountSession.BearerToken(Request);
        if (bearer is null && !WebAccountSession.IsSameOrigin(Request))
            return StatusCode(403, new { message = "请从本站退出登录。" });
        string? token = bearer ?? WebAccountSession.CookieToken(Request);
        if (token is not null)
        {
            try { await accounts.LogoutAsync(token, cancellationToken); }
            catch (FormatException) { }
        }
        if (bearer is null) WebAccountSession.Clear(HttpContext);
        return Ok(new { message = "已退出登录。" });
    }

    private static object Profile(AccountResult result) => new
    {
        result.Username, result.DisplayName, result.Domain, result.Role, result.UserId,
        result.Permissions, result.ExpiresAt, result.Message
    };
}
