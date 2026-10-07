using Microsoft.AspNetCore.Mvc;
using Scanner.Server.BLL;
using Scanner.Server.Model;
using Scanner.Web.Filters;
namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/tester-pallet-scans")]
[ApiKey]
public sealed class PalletScansController(IPalletScanService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PalletScanStoredRow>>> GetAll(CancellationToken cancellationToken)
        => Ok(await service.GetAllAsync(cancellationToken));
    [HttpPost]
    public async Task<ActionResult<PalletScanBatchResult>> Upload([FromBody] PalletScanBatchRequest request, CancellationToken cancellationToken)
        => Ok(await service.UploadAsync(request, cancellationToken));
}
