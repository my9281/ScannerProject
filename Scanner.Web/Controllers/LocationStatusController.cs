using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Scanner.Server.DAL;
using Scanner.Web.Filters;
using Scanner.Web.Services;
namespace Scanner.Web.Controllers;

[ApiController, Route("api/warehouse-locations/status")]
public sealed class LocationStatusController(IMySqlConnectionFactory connections) : ControllerBase
{
    [HttpPut, AccountPermission("system.configure")]
    public async Task<IActionResult> SetStatus(LocationStatusRequest request, CancellationToken ct)
    {
        var id = request.LocationId.Trim();
        var reason = request.Reason.Trim();
        if (id.Length is < 1 or > 100 || id.Any(char.IsControl) || reason.Length is < 1 or > 500)
            throw new ArgumentException("库位 ID 或变更原因无效。");
        var account = (AccountResult)HttpContext.Items[typeof(AccountResult)]!;
        if (account.UserId == 0) return Unauthorized();
        await using var connection = await connections.OpenConnectionAsync(ct);
        await using var tx = await connection.BeginTransactionAsync(ct);
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.Parameters.AddWithValue("@id",id);
        command.CommandText = "SELECT is_disabled FROM warehouse_locations WHERE location_id=@id FOR UPDATE;";
        var exists = await command.ExecuteScalarAsync(ct);
        if (exists is null) return NotFound(new { message="库位不存在。" });
        command.Parameters.AddWithValue("@disabled",request.IsDisabled!.Value ? 1 : 0);
        command.CommandText = "UPDATE warehouse_locations SET is_disabled=@disabled WHERE location_id=@id;";
        await command.ExecuteNonQueryAsync(ct);
        command.Parameters.AddWithValue("@operator",account.UserId);
        command.Parameters.AddWithValue("@reason",reason);
        command.CommandText = "INSERT INTO warehouse_location_status_history (location_id,is_disabled,operator_user_id,reason) VALUES (@id,@disabled,@operator,@reason);";
        await command.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return Ok(new { locationId=id,isDisabled=request.IsDisabled.Value });
    }
}
public sealed class LocationStatusRequest
{
    [Required,StringLength(100)] public string LocationId { get; init; } = "";
    [Required] public bool? IsDisabled { get; init; }
    [Required,StringLength(500)] public string Reason { get; init; } = "";
}
