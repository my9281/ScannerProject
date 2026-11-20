namespace Scanner.Web.Services;

internal static class WebAccountSession
{
    private const string CookieName = "repair_session";
    internal static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    internal static string? CookieToken(HttpRequest request) => request.Cookies[CookieName];

    internal static string? BearerToken(HttpRequest request)
    {
        string value = request.Headers.Authorization.ToString();
        return value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? value[7..].Trim() : null;
    }

    internal static bool IsSameOrigin(HttpRequest request)
    {
        string site = request.Headers["Sec-Fetch-Site"].ToString();
        string origin = request.Headers.Origin.ToString();
        if (site.Length != 0) return site == "same-origin";
        return origin.Length == 0 || string.Equals(origin,
            $"{request.Scheme}://{request.Host.ToUriComponent()}", StringComparison.OrdinalIgnoreCase);
    }

    internal static async Task<AccountResult?> ResolveAsync(HttpContext context, IAccountService accounts)
    {
        string? bearer = BearerToken(context.Request);
        string? key = bearer ?? CookieToken(context.Request);
        if (string.IsNullOrEmpty(key)) return null;
        if (bearer is null && !HttpMethods.IsGet(context.Request.Method) &&
            !HttpMethods.IsHead(context.Request.Method) && !IsSameOrigin(context.Request)) return null;

        AccountResult? account;
        try
        {
            account = bearer is null
                ? await accounts.RenewWebSessionAsync(key, context.RequestAborted)
                : await accounts.GetCurrentAsync(key, context.RequestAborted);
        }
        catch (FormatException) { account = null; }

        if (bearer is null)
        {
            if (account is null) Clear(context);
            else Write(context, account);
        }
        return account;
    }

    internal static void Write(HttpContext context, AccountResult account)
    {
        var options = Options(context);
        options.Expires = account.ExpiresAt;
        context.Response.Cookies.Append(CookieName, account.Token, options);
    }

    internal static void Clear(HttpContext context) => context.Response.Cookies.Delete(CookieName, Options(context));

    private static CookieOptions Options(HttpContext context) => new()
    {
        HttpOnly = true,
        Secure = context.Request.IsHttps || !context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true
    };
}
