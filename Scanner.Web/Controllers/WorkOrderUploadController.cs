using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Scanner.Web.Filters;
using Scanner.Web.Services;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/checklist-work-orders/batch")]
[ApiKey]
public sealed class WorkOrderUploadController(WorkOrderBatchService batches, ILogger<WorkOrderUploadController> logger) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(4 * 1024 * 1024)]
    public async Task<IActionResult> Upload(WorkOrderBatchRequest request, CancellationToken cancellationToken)
    {
        try { return Ok(await batches.UploadAsync(request, cancellationToken)); }
        catch (MySqlException exception)
        {
            string traceId = HttpContext.TraceIdentifier;
            logger.LogError(exception, "Work-order batch upload database failure. MySQL error {ErrorNumber}, trace {TraceId}", exception.Number, traceId);
            var (status, message) = WorkOrderUploadErrors.Describe(exception.Number);
            return StatusCode(status, new { message = message + " 追踪号：" + traceId, traceId });
        }
    }
}

public static class WorkOrderUploadErrors
{
    public static (int Status, string Message) Describe(int number) => number switch
    {
        1146 => (503, "工单上传依赖的数据库表不存在，请在 Web 使用的数据库执行 004_checklist_work_orders.sql 和 006_work_order_sources.sql。"),
        1054 or 1364 => (503, "工单数据库字段与接口不兼容，请对照 004、006 SQL 脚本检查实际表结构；CREATE TABLE IF NOT EXISTS 不会更新已有表。"),
        1142 or 1143 => (503, "Web 数据库账号没有工单上传所需的读写权限，请检查 SELECT、INSERT、UPDATE 权限。"),
        1042 or 1045 or 1049 or 2002 or 2003 => (503, "无法连接工单数据库，请检查 Web 的数据库连接配置和数据库运行状态。"),
        1205 or 1213 => (503, "工单数据库繁忙，请稍后重试上传；相同工单会自动去重。"),
        _ => (500, "工单入库失败，请使用追踪号查询 Web 服务端日志。")
    };
}
