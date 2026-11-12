using Microsoft.AspNetCore.Mvc;
using Scanner.Server.DAL;
using Scanner.Server.Model;
using Scanner.Web.Filters;

namespace Scanner.Web.Controllers;

[ApiController, Route("api/repair-scans"), ApiKey]
public sealed class RepairScansController(IMySqlConnectionFactory connections) : ControllerBase
{
    [HttpPost, RequestSizeLimit(4096)]
    public async Task<IActionResult> Upload(RepairScan scan, CancellationToken ct)
    {
        string sn = scan.Sn?.Trim() ?? "", sku = scan.Sku?.Trim() ?? "";
        if (scan.ScanId == Guid.Empty || sn.Length is < 1 or > 50 || sku.Length is < 1 or > 50
            || sn.Any(char.IsControl) || sku.Any(char.IsControl) || scan.StartedAt == default
            || scan.StartedAt > DateTimeOffset.UtcNow.AddMinutes(5))
            return BadRequest(new { message = "SN、SKU 必须各为 1–50 个字符，且扫描时间有效。" });
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.Parameters.AddWithValue("@id", scan.ScanId.ToString("D"));
        command.Parameters.AddWithValue("@sn", sn);
        command.Parameters.AddWithValue("@sku", sku);
        // Match the millisecond precision of DATETIME(3), including on retry.
        var started = DateTimeOffset.FromUnixTimeMilliseconds(scan.StartedAt.ToUnixTimeMilliseconds()).UtcDateTime;
        command.Parameters.AddWithValue("@started", started);
        command.CommandText = "INSERT INTO repair_scan_records (scan_id,sn,sku,started_at,status) VALUES (@id,@sn,@sku,@started,'开始维修') ON DUPLICATE KEY UPDATE scan_id=scan_id;";
        await command.ExecuteNonQueryAsync(ct);
        command.CommandText = "SELECT sn,sku,started_at FROM repair_scan_records WHERE scan_id=@id FOR UPDATE;";
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct) || reader.GetString(0) != sn || reader.GetString(1) != sku || reader.GetDateTime(2) != started)
                return Conflict(new { message = "扫描编号已用于其他记录。" });
        }
        await tx.CommitAsync(ct);
        return Ok(new { scanId = scan.ScanId, status = "开始维修" });
    }
}
