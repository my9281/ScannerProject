using Microsoft.AspNetCore.Mvc;
using Scanner.Server.Model;
using Scanner.Web.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController, Route("api/carrier-receipts")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CarrierReceiptController(CarrierReceiptService receipts) : ControllerBase
{
    private AccountResult Account => (AccountResult)HttpContext.Items[typeof(AccountResult)]!;
    [HttpGet, AccountPermission("receiving.read")]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int page = 0, [FromQuery] string search = "") =>
        Ok(await receipts.List(Account.Domain.Id, page, search, ct));
    [HttpGet("context"), AccountPermission("receiving.write")]
    public async Task<IActionResult> Context(CancellationToken ct) => Ok(await receipts.Context(Account, ct));
    [HttpPost, AccountPermission("receiving.write"), RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Upload(CarrierReceiptBatch batch, CancellationToken ct) => Ok(await receipts.Upload(batch, Account, ct));
}
