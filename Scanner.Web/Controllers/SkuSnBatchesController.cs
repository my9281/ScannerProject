using Microsoft.AspNetCore.Mvc;
using Scanner.Server.BLL;
using Scanner.Server.DAL;
using Scanner.Server.Model;
using Scanner.Web.Filters;
namespace Scanner.Web.Controllers;
[ApiController, Route("api/sku-sn-batches"), ApiKey]
public sealed class SkuSnBatchesController(SkuSnBatchRepository repository) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Upload(SkuSnBatch batch, CancellationToken ct)
    {
        var valid = SkuSnBatchValidator.Validate(batch);
        await repository.SaveAsync(valid, ct);
        return Ok(new { valid.BatchId, ItemCount = valid.Items.Count });
    }
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await repository.ListAsync(ct));
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var batch = await repository.GetAsync(id, ct);
        return batch is null ? NotFound(new { message = "批次不存在。" }) : Ok(batch);
    }
}
