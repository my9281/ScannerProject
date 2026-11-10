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
    [HttpGet("lookup")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<PalletScanStoredRow>> Lookup([FromQuery] string sn, CancellationToken cancellationToken)
    {
        var row = await service.FindBySnAsync(sn, cancellationToken);
        return row is null ? NotFound(new { message = "未找到该 SN 的托盘记录。" }) : Ok(row);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PalletScanStoredRow>>> GetAll(CancellationToken cancellationToken)
        => Ok(await service.GetAllAsync(cancellationToken));
    [HttpPost]
    public async Task<ActionResult<PalletScanBatchResult>> Upload([FromBody] PalletScanBatchRequest request, CancellationToken cancellationToken)
        => Ok(await service.UploadAsync(request, cancellationToken));
}
