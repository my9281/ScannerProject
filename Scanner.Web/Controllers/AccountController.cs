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
        AccountResult? result = await accounts.LoginAsync(request, cancellationToken);
        return result is null
            ? Unauthorized(new { message = "用户名或密码错误。" })
            : Ok(result);
    }

    [HttpGet("me")]
    public async Task<ActionResult<AccountResult>> Me(CancellationToken cancellationToken)
    {
        string? token = BearerToken();
        if (token is null) return Unauthorized(new { message = "请先登录。" });
        AccountResult? result;
        try { result = await accounts.GetCurrentAsync(token, cancellationToken); }
        catch (FormatException) { result = null; }
        return result is null ? Unauthorized(new { message = "登录已失效，请重新登录。" }) : Ok(result);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        string? token = BearerToken();
        if (token is not null)
        {
            try { await accounts.LogoutAsync(token, cancellationToken); }
            catch (FormatException) { }
        }
        return Ok(new { message = "已退出登录。" });
    }

    private string? BearerToken()
    {
        string value = Request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : null;
    }
}
