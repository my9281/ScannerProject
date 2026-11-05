using Microsoft.AspNetCore.Mvc;
using Scanner.Server.DAL;
using Scanner.Server.Model;
using Scanner.Web.Filters;
namespace Scanner.Web.Controllers;

[ApiController, Route("api/warehouse-locations/skus"), ApiKey]
public sealed class LocationSkusController(IMySqlConnectionFactory connections) : ControllerBase
{
    private static string Clean(string? value) => value?.Trim() is { Length: > 0 and <= 100 } s && !s.Any(char.IsControl)
        ? s : throw new ArgumentException("代码和操作人必须为 1–100 个字符，不能包含控制字符。");

    [HttpGet]
    public async Task<IActionResult> Get(string locationId, CancellationToken ct)
    {
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var cmd = connection.CreateCommand();
        cmd.Parameters.AddWithValue("@id", Clean(locationId));
        cmd.CommandText = "SELECT is_disabled FROM warehouse_locations WHERE location_id=@id";
        var disabled = await cmd.ExecuteScalarAsync(ct);
        if (disabled is null) return NotFound(new { message = "库位不存在，请先创建并上传。" });
        if (Convert.ToBoolean(disabled)) return BadRequest(new { message = "库位已禁用。" });
        cmd.CommandText = "SELECT sku,quantity FROM warehouse_location_skus WHERE location_id=@id ORDER BY sku";
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<LocationSkuRow>();
        while (await reader.ReadAsync(ct)) rows.Add(new(reader.GetString(0), reader.GetInt64(1)));
        return Ok(rows);
    }

    [HttpPost("bind")]
    public Task<IActionResult> Bind(LocationSkuChange request, CancellationToken ct) => Save(request, true, ct);
    [HttpPost("adjust")]
    public Task<IActionResult> Adjust(LocationSkuChange request, CancellationToken ct) => Save(request, false, ct);

    private async Task<IActionResult> Save(LocationSkuChange request, bool bind, CancellationToken ct)
    {
        var id = Clean(request.LocationId); var sku = Clean(request.Sku); var actor = Clean(request.Operator);
        if (request.OperationId == Guid.Empty || (bind ? request.Delta != 0 : request.Delta == 0))
            return BadRequest(new { message = "操作编号或数量无效。" });
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var cmd = connection.CreateCommand(); cmd.Transaction = tx;
        cmd.Parameters.AddWithValue("@id", id); cmd.Parameters.AddWithValue("@sku", sku);
        cmd.Parameters.AddWithValue("@op", request.OperationId.ToString());
        cmd.Parameters.AddWithValue("@actor", actor); cmd.Parameters.AddWithValue("@delta", request.Delta);
        cmd.Parameters.AddWithValue("@action", bind ? "bind" : "adjust");
        cmd.CommandText = "SELECT is_disabled FROM warehouse_locations WHERE location_id=@id FOR UPDATE";
        var disabled = await cmd.ExecuteScalarAsync(ct);
        if (disabled is null) return NotFound(new { message = "库位不存在，请先创建并上传。" });
        if (Convert.ToBoolean(disabled)) return BadRequest(new { message = "库位已禁用。" });
        cmd.CommandText = "SELECT COUNT(*) FROM warehouse_location_sku_history WHERE operation_id=@op";
        if (Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)) > 0)
        {
            cmd.CommandText = "SELECT quantity_after FROM warehouse_location_sku_history WHERE operation_id=@op AND location_id=@id AND sku=@sku AND action=@action AND delta=@delta AND operator_name=@actor";
            var previous = await cmd.ExecuteScalarAsync(ct);
            return previous is null ? Conflict(new { message = "操作编号已被其他操作使用。" }) : Ok(new LocationSkuRow(sku, Convert.ToInt64(previous)));
        }
        if (bind)
        {
            cmd.CommandText = "INSERT INTO warehouse_location_skus(location_id,sku) VALUES(@id,@sku) ON DUPLICATE KEY UPDATE sku=sku";
            await cmd.ExecuteNonQueryAsync(ct);
        }
        cmd.CommandText = "SELECT quantity FROM warehouse_location_skus WHERE location_id=@id AND sku=@sku FOR UPDATE";
        var current = await cmd.ExecuteScalarAsync(ct);
        if (current is null) return BadRequest(new { message = "SKU 未绑定到此库位，请先核对绑定。" });
        var before = Convert.ToInt64(current); var after = checked(before + request.Delta);
        if (after < 0) return BadRequest(new { message = "数量不足，不能减成负数。" });
        cmd.Parameters.AddWithValue("@before", before); cmd.Parameters.AddWithValue("@after", after);
        cmd.CommandText = "UPDATE warehouse_location_skus SET quantity=@after WHERE location_id=@id AND sku=@sku";
        await cmd.ExecuteNonQueryAsync(ct);
        cmd.CommandText = "INSERT INTO warehouse_location_sku_history(operation_id,location_id,sku,action,delta,quantity_before,quantity_after,operator_name) VALUES(@op,@id,@sku,@action,@delta,@before,@after,@actor)";
        await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new LocationSkuRow(sku, after));
    }
}
