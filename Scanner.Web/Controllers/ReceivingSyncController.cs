using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController, Route("api/receiving-sync"), AccountPermission("system.configure")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReceivingSyncController(ReceivingSyncStore store) : ControllerBase
{
    private AccountResult Account => (AccountResult)HttpContext.Items[typeof(AccountResult)]!;
    [HttpGet("settings")]
    public async Task<IActionResult> Settings(CancellationToken ct) => Ok(await store.Settings(Account.Domain.Id, ct));
    [HttpPut("settings"), RequestSizeLimit(16384)]
    public async Task<IActionResult> Save(ReceivingSyncEdit edit, CancellationToken ct)
    {
        await store.Save(Account.Domain.Id, Account.Username, edit, ct);
        return Ok(new { message = "已保存，后台最多约 15 秒后检查新配置；运行中的任务完成后才会执行下一任务。" });
    }
    [HttpPost("run")]
    public async Task<IActionResult> Run(CancellationToken ct)
    {
        await store.Queue(Account.Domain.Id, ct);
        return Accepted(new { message = "已请求同步，请查看最后成功时间和同步结果。" });
    }
    [HttpGet("forecasts")]
    public async Task<IActionResult> Forecasts(CancellationToken ct, [FromQuery] ulong before = 0) => Ok(await store.Forecasts(Account.Domain.Id, before, ct));
}
