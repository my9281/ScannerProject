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
    [HttpGet("version")]
    public IActionResult Version() => Ok(new { version = "receipt-scan-separators-20261119-v6" });
    [HttpGet, AccountPermission("receiving.read")]
    public async Task<IActionResult> List(CancellationToken ct, [FromQuery] int page = 0, [FromQuery] string search = "") =>
        Ok(await receipts.List(Account.Domain.Id, page, search, board: false, ct));
    [HttpGet("board"), AccountPermission("receiving.read")]
    public async Task<IActionResult> Board(CancellationToken ct, [FromQuery] int page = 0, [FromQuery] string search = "") =>
        Ok(await receipts.List(Account.Domain.Id, page, search, board: true, ct));
    [HttpGet("context"), AccountPermission("receiving.write", AllowPdaReceiver = true)]
    public async Task<IActionResult> Context(CancellationToken ct) => Ok(await receipts.Context(Account, ct));
    [HttpPost, AccountPermission("receiving.write", AllowPdaReceiver = true), RequestSizeLimit(1024 * 1024)]
    public async Task<IActionResult> Upload(CarrierReceiptBatch batch, CancellationToken ct) => Ok(await receipts.Upload(batch, Account, ct));
}
