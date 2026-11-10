using System.ComponentModel.DataAnnotations;
using MySqlConnector;
using Scanner.Server.DAL;

namespace Scanner.Web.Services;

public sealed class WorkOrderBatchRequest
{
    [Required, MinLength(1), MaxLength(500)] public List<WorkOrderUploadItem> Items { get; init; } = new();
}
public sealed class WorkOrderUploadItem
{
    [Required, RegularExpression("^[a-f0-9]{64}$")] public string SourceKey { get; init; } = "";
    [Required, StringLength(100)] public string DeviceId { get; init; } = "";
    public ulong? CycleNumber { get; init; }
    [StringLength(100)] public string Location { get; init; } = "";
    [Required, StringLength(1000)] public string Description { get; init; } = "";
}
public sealed record WorkOrderBatchResult(int InsertedCount, int SkippedCount, ulong UserId, string Username);

public sealed class WorkOrderBatchService(IMySqlConnectionFactory connections, IConfiguration? configuration = null)
{
    public async Task<WorkOrderBatchResult> UploadAsync(WorkOrderBatchRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count is < 1 or > 500 || request.Items.Any(x => x is null ||
            !Validator.TryValidateObject(x, new ValidationContext(x), null, true) || string.IsNullOrWhiteSpace(x.DeviceId) || string.IsNullOrWhiteSpace(x.Description)))
            throw new ArgumentException("每批需包含 1–500 条有效工单，设备 ID 最长 100 字，描述最长 1000 字。");
        string targetUsername = configuration?["WorkOrders:DefaultUsername"]?.Trim() ?? "my9281";
        if (targetUsername.Length is < 1 or > 100) throw new ArgumentException("工单接收账号配置无效。");
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT u.`id` FROM `app_users` u INNER JOIN `user_domains` d ON d.`id`=u.`domain_id` WHERE u.`username`=@username AND u.`status`='active' AND d.`is_enabled`=1 FOR UPDATE;";
        command.Parameters.AddWithValue("@username", targetUsername);
        var target = await command.ExecuteScalarAsync(cancellationToken);
        if (target is null || target is DBNull) throw new ArgumentException("配置的工单接收用户不存在或未启用，请先检查用户及所属域。");
        ulong userId = Convert.ToUInt64(target);
        int inserted = 0;
        // Stable ordering also avoids deadlocks when concurrent batches overlap.
        foreach (var item in request.Items.OrderBy(x => x.DeviceId, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.SourceKey, StringComparer.Ordinal))
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@sn", item.DeviceId.Trim());
            command.CommandText = "INSERT INTO inventory_device_state (sn,cycle_number,is_shelved) VALUES (@sn,1,0) ON DUPLICATE KEY UPDATE sn=sn;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "SELECT cycle_number,is_shelved FROM inventory_device_state WHERE sn=@sn FOR UPDATE;";
            ulong cycle;
            bool shelved;
            await using (var state = await command.ExecuteReaderAsync(cancellationToken))
            {
                await state.ReadAsync(cancellationToken);
                cycle = state.GetUInt64(0); shelved = state.GetBoolean(1);
            }
            if (shelved) continue;
            if ((item.CycleNumber.HasValue && item.CycleNumber.Value != cycle) || (!item.CycleNumber.HasValue && cycle != 1))
                throw new ArgumentException("工单来源作业周期已变化，请重新下载良品区并匹配后上传。");
            command.Parameters.AddWithValue("@key", cycle == 1 ? item.SourceKey : Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(item.SourceKey + ":cycle:" + cycle))).ToLowerInvariant());
            command.CommandText = "INSERT INTO `checklist_work_order_sources` (`source_key`) VALUES (@key) ON DUPLICATE KEY UPDATE `source_key`=`source_key`;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "SELECT `work_order_id` FROM `checklist_work_order_sources` WHERE `source_key`=@key FOR UPDATE;";
            var existing = await command.ExecuteScalarAsync(cancellationToken);
            if (existing is not null && existing is not DBNull) continue;

            command.Parameters.AddWithValue("@cycle", cycle);
            command.Parameters.AddWithValue("@user", userId);
            command.Parameters.AddWithValue("@device", item.DeviceId.Trim());
            command.Parameters.AddWithValue("@description", item.Description.Trim());
            command.Parameters.AddWithValue("@location", (item.Location ?? "").Trim());
            command.CommandText = "INSERT INTO `checklist_work_orders` (`user_id`,`device_id`,`description`,`location`,`cycle_number`,`task_at`,`is_completed`,`audit_status`) VALUES (@user,@device,@description,@location,@cycle,UTC_TIMESTAMP(3),0,0);";
            await command.ExecuteNonQueryAsync(cancellationToken);
            long id = command.LastInsertedId;
            command.Parameters.AddWithValue("@id", id);
            command.CommandText = "UPDATE `checklist_work_order_sources` SET `work_order_id`=@id WHERE `source_key`=@key;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            inserted++;
        }
        await transaction.CommitAsync(cancellationToken);
        return new(inserted, request.Items.Count - inserted, userId, targetUsername);
    }
}
