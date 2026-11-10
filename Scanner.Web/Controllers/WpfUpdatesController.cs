using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController]
public sealed class WpfUpdatesController(WpfUpdateService updates, ILogger<WpfUpdatesController> logger) : ControllerBase
{
    [HttpGet("/updates/wpf/latest.json")]
    public async Task<IActionResult> Latest(CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        var manifest = await updates.GetLatestAsync(token);
        return manifest == null ? NotFound(new { message = "尚未发布 WPF 更新包。" }) : Ok(manifest);
    }

    [HttpGet("/updates/wpf/packages/{name}")]
    public IActionResult Package(string name)
    {
        string? path = updates.GetPackagePath(name);
        if (path == null) return NotFound();
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(path, "application/zip", name, enableRangeProcessing: true);
    }

    [HttpPost("/api/updates/wpf")]
    [AccountPermission("system.configure")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(WpfUpdateService.MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = WpfUpdateService.MaxRequestBytes)]
    public async Task<IActionResult> Publish([FromForm] IFormFile file, CancellationToken token)
    {
        Response.Headers.CacheControl = "no-store";
        var manifest = await updates.PublishAsync(file, token);
        logger.LogInformation("WPF release {Version} published by {User}; SHA256={Hash}", manifest.Version, User.Identity?.Name, manifest.Sha256);
        return Ok(new { message = "更新包已发布，WPF 下次启动时会自动检查。", manifest });
    }
}
