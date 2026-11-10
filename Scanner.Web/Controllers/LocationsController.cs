using Microsoft.AspNetCore.Mvc;
using Scanner.Server.BLL;
using Scanner.Server.Model;
using Scanner.Web.Filters;
namespace Scanner.Web.Controllers;
[ApiController]
[Route("api/warehouse-locations")]
[ApiKey]
public sealed class LocationsController(ILocationService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<LocationBatchResult>> Upload([FromBody] LocationBatchRequest request, CancellationToken cancellationToken) => Ok(await service.UploadAsync(request, cancellationToken));
    [HttpGet]
    [HttpGet("download")]
    public async Task<ActionResult<IReadOnlyList<LocationRow>>> Download(CancellationToken cancellationToken) => Ok(await service.GetAllAsync(cancellationToken));
}
