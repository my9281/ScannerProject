using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/checklist-work-orders")]
public sealed class WorkOrdersController(IWorkOrderService orders) : ControllerBase
{
    [HttpGet]
    [AccountPermission("checklist.read")]
    public async Task<IActionResult> Download([FromQuery] ulong? userId, [FromQuery] ulong afterId = 0, CancellationToken cancellationToken = default)
    {
        Response.Headers.CacheControl = "no-store";
        var account = (AccountResult)HttpContext.Items[typeof(AccountResult)]!;
        if (account.UserId == 0 || userId.HasValue && userId != account.UserId)
            return StatusCode(403, new { message = "只能下载当前登录用户的工单。" });
        return Ok(await orders.DownloadAsync(account.UserId, afterId, cancellationToken));
    }
    [HttpPut("{id}")]
    [AccountPermission("checklist.write")]
    public async Task<IActionResult> Update(ulong id, [FromBody] UpdateWorkOrderRequest request, CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-store";
        var account = (AccountResult)HttpContext.Items[typeof(AccountResult)]!;
        if (account.UserId == 0) return Unauthorized(new { message = "请重新登录。" });
        var result = await orders.UpdateAsync(account.UserId, id, request.IsCompleted!.Value, request.Version!.Value, cancellationToken);
        return result is null ? Conflict(new { message = "工单已变更、停止下发或不可访问，请重新下载后再试。" }) : Ok(result);
    }
}

public sealed class UpdateWorkOrderRequest
{
    [Required] public bool? IsCompleted { get; init; }
    [Required] public uint? Version { get; init; }
}
