using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/checklist-work-orders/batch")]
[ApiKey]
public sealed class WorkOrderUploadController(WorkOrderBatchService batches) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Upload(WorkOrderBatchRequest request, CancellationToken cancellationToken)
        => Ok(await batches.UploadAsync(request, cancellationToken));
}
