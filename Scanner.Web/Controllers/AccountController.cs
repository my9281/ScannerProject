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
    public ActionResult<IReadOnlyList<AccountDomain>> GetDomains() => Ok(accounts.GetDomains());

    [HttpPost("register")]
    [ProducesResponseType<AccountResult>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public ActionResult<AccountResult> Register([FromBody] RegisterRequest request)
    {
        try
        {
            AccountResult result = accounts.Register(request);
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
    public ActionResult<AccountResult> Login([FromBody] LoginRequest request)
    {
        AccountResult? result = accounts.Login(request);
        return result is null
            ? Unauthorized(new { message = "用户名或密码错误。" })
            : Ok(result);
    }
}
