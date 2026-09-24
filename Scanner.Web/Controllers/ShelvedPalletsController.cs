using Microsoft.AspNetCore.Mvc;
using Scanner.Server.BLL;
using Scanner.Server.Model;
using Scanner.Web.Filters;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/shelved-pallets")]
[ApiKey]
public sealed class ShelvedPalletsController(IShelvedPalletService pallets) : ControllerBase
{
    [HttpGet("all")]
    public async Task<ActionResult<ShelvedPalletTableResult>> GetAll(CancellationToken cancellationToken)
        => Ok(await pallets.GetAllAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CreateShelvedPalletBatchResult>> Create(
        [FromBody] CreateShelvedPalletBatchRequest request,
        CancellationToken cancellationToken)
        => Ok(await pallets.CreateBatchAsync(request, cancellationToken));

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ShelvedPalletRecord>>> Query(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? palletNumber,
        [FromQuery] DateOnly? date,
        [FromQuery(Name = "t")] DateOnly? shortDate,
        [FromQuery(Name = "p")] string? shortPalletNumber,
        [FromQuery] int limit = 500,
        CancellationToken cancellationToken = default)
    {
        date ??= shortDate;
        if (date.HasValue)
        {
            from ??= date.Value.ToDateTime(TimeOnly.MinValue);
            to ??= date.Value.ToDateTime(TimeOnly.MaxValue);
        }
        palletNumber = string.IsNullOrWhiteSpace(palletNumber) ? shortPalletNumber : palletNumber;
        return Ok(await pallets.QueryAsync(from, to, palletNumber, limit, cancellationToken));
    }
}
