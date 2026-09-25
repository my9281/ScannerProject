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
    if (context.Request.Path.Equals("/pallet-data.html", StringComparison.OrdinalIgnoreCase))
    {
        string date = context.Request.Query["t"].ToString();
        string pallet = context.Request.Query["p"].ToString();
        if (!DateOnly.TryParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out _) || string.IsNullOrWhiteSpace(pallet))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
    }
    else if (context.Request.Path.Equals("/pallet-directory.html", StringComparison.OrdinalIgnoreCase))
    {
        string start = context.Request.Query["ts"].ToString();
        string end = context.Request.Query["te"].ToString();
        bool validStart = DateOnly.TryParseExact(start, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out DateOnly startDate);
        bool validEnd = DateOnly.TryParseExact(end, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out DateOnly endDate);
        if (!validStart || !validEnd || startDate > endDate)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
    }
    await next();
});
app.UseStaticFiles();
app.MapControllers();
app.Run();

public partial class Program;
