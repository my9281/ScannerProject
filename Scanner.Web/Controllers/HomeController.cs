using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController]
public sealed class HomeController(HomeMetricsService metrics) : ControllerBase
{
    [HttpGet("/api/home/metrics")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<HomeMetrics>> Get(CancellationToken cancellationToken)
        => Ok(await metrics.GetAsync(cancellationToken));
}
