using Microsoft.AspNetCore.Mvc;
using Scanner.Server.BLL;
using Scanner.Web.Filters;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/tester-pallet-view")]
[AccountPermission("system.configure")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TesterPalletViewController(IPalletScanService scans) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await scans.GetAllAsync(ct));
}
