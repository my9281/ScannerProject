using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Scanner.Web.Services;
using System.Security.Claims;

namespace Scanner.Web.Filters;

// Apply to future CheckListBoard business endpoints; API keys do not grant user permissions.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AccountPermissionAttribute(string permission) : Attribute, IAsyncAuthorizationFilter
{
    public bool AllowPdaReceiver { get; set; }
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        context.HttpContext.Response.Headers.CacheControl = "no-store";
        var service = context.HttpContext.RequestServices.GetRequiredService<IAccountService>();
        AccountResult? account = await WebAccountSession.ResolveAsync(context.HttpContext, service);
        if (account is null)
            context.Result = new UnauthorizedObjectResult(new { message = "请先登录或重新登录。" });
        else if (!account.Permissions.Contains(permission, StringComparer.Ordinal)
            && !(AllowPdaReceiver && permission == "receiving.write" && account.Username.Equals("PDA01", StringComparison.OrdinalIgnoreCase)))
            context.Result = new ObjectResult(new { message = "当前账户没有此操作权限。" }) { StatusCode = 403 };
        else
        {
            context.HttpContext.Items[typeof(AccountResult)] = account;
            context.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.Name, account.Username),
                new Claim(ClaimTypes.NameIdentifier, account.UserId.ToString()),
                new Claim(ClaimTypes.Role, account.Role),
                new Claim("domain_id", account.Domain.Id.ToString())
            ], "AccountSession"));
        }
    }
}
