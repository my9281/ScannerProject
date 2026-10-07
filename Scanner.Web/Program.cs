using Microsoft.AspNetCore.HttpOverrides;
using Scanner.Web.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();
if (args.Length > 0) builder.Configuration.AddCommandLine(args);
builder.Services.AddScannerWeb(builder.Configuration);

WebApplication app = builder.Build();
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseDefaultFiles();
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Robots-Tag"] = "noindex, nofollow, noarchive";
    // Public site exposes only the presentation and account sign-in flow.
    // Device and desktop APIs retain their existing authentication and behavior.
    string path = (context.Request.Path.Value ?? "").TrimEnd('/');
    if (path.Equals("/register.html", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/account/register", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/account/register", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/api/account/domains", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/pallet-data.html", StringComparison.OrdinalIgnoreCase)
        || path.Equals("/pallet-directory.html", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});
app.UseStaticFiles();
app.MapControllers();
app.Run();

public partial class Program;
