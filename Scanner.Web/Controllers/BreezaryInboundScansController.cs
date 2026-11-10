using Microsoft.AspNetCore.Mvc;
using Scanner.Server.Model;
using Scanner.Web.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/breezary-inbound-scans")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class BreezaryInboundScansController(BreezaryInboundService scans) : ControllerBase
{
    [HttpPost]
    [ApiKey]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload([FromBody] BreezaryInboundBatch batch, CancellationToken ct) =>
        Ok(await scans.UploadAsync(batch, ct));

    [HttpGet]
    [AccountPermission("system.configure")]
    public async Task<IActionResult> List([FromQuery] ulong beforeId = 0, [FromQuery] int limit = 100,
        [FromQuery] int? location = null, [FromQuery] string? search = null, CancellationToken ct = default) =>
        Ok(await scans.ListAsync(beforeId, limit, location, search, ct));
}
