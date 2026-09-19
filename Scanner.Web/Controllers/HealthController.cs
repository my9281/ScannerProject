using Microsoft.AspNetCore.Mvc;
using Scanner.Server.BLL;
using Scanner.Server.Model;

namespace Scanner.Web.Controllers;

[ApiController]
public sealed class HealthController(IDatabaseHealthService databaseHealth) : ControllerBase
{
    [HttpGet("/health")]
    public IActionResult Health() => Ok(new { status = "ok", utc = DateTimeOffset.UtcNow });

    [HttpGet("/health/database")]
    public async Task<IActionResult> Database(CancellationToken cancellationToken)
    {
        DatabaseHealthStatus status = await databaseHealth.CheckAsync(cancellationToken);
        return status.IsHealthy ? Ok(status) : StatusCode(StatusCodes.Status503ServiceUnavailable, status);
    }
}
