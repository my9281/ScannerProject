using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Scanner.Web.Services;
using System.Security.Cryptography;
using System.Text;

namespace Scanner.Web.Filters;

public sealed class ApiKeyAuthorizationFilter(IOptions<UploadOptions> options) : IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        string expected = options.Value.ApiKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(expected)) return;
        string supplied = context.HttpContext.Request.Headers["X-Upload-Key"].ToString();
        if (!KeysEqual(expected, supplied)) context.Result = new UnauthorizedObjectResult(new { message = "访问密钥无效。" });
    }

    private static bool KeysEqual(string expected, string actual)
    {
        byte[] left = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        byte[] right = SHA256.HashData(Encoding.UTF8.GetBytes(actual));
        return CryptographicOperations.FixedTimeEquals(left, right);
    }
}
