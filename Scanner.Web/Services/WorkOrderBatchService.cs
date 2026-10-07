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
    [Required, StringLength(1000)] public string Description { get; init; } = "";
}
public sealed record WorkOrderBatchResult(int InsertedCount, int SkippedCount, ulong UserId, string Username);

public sealed class WorkOrderBatchService(IMySqlConnectionFactory connections)
{
    public async Task<WorkOrderBatchResult> UploadAsync(WorkOrderBatchRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count is < 1 or > 500 || request.Items.Any(x => x is null ||
            !Validator.TryValidateObject(x, new ValidationContext(x), null, true) || string.IsNullOrWhiteSpace(x.DeviceId) || string.IsNullOrWhiteSpace(x.Description)))
            throw new ArgumentException("每批需包含 1–500 条有效工单，设备 ID 最长 100 字，描述最长 1000 字。");
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT u.`id` FROM `app_users` u INNER JOIN `user_domains` d ON d.`id`=u.`domain_id` WHERE u.`username`='my9281' AND u.`status`='active' AND d.`is_enabled`=1 FOR UPDATE;";
        var target = await command.ExecuteScalarAsync(cancellationToken);
        if (target is null || target is DBNull) throw new ArgumentException("默认接收用户 my9281 不存在或未启用，请先检查用户及所属域。");
        ulong userId = Convert.ToUInt64(target);
        int inserted = 0;
        // Stable ordering also avoids deadlocks when concurrent batches overlap.
        foreach (var item in request.Items.OrderBy(x => x.SourceKey, StringComparer.Ordinal))
        {
            command.Parameters.Clear();
            command.Parameters.AddWithValue("@key", item.SourceKey);
            command.CommandText = "INSERT INTO `checklist_work_order_sources` (`source_key`) VALUES (@key) ON DUPLICATE KEY UPDATE `source_key`=`source_key`;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            command.CommandText = "SELECT `work_order_id` FROM `checklist_work_order_sources` WHERE `source_key`=@key FOR UPDATE;";
            var existing = await command.ExecuteScalarAsync(cancellationToken);
            if (existing is not null && existing is not DBNull) continue;
            command.Parameters.AddWithValue("@user", userId);
            command.Parameters.AddWithValue("@device", item.DeviceId.Trim());
            command.Parameters.AddWithValue("@description", item.Description.Trim());
            command.CommandText = "INSERT INTO `checklist_work_orders` (`user_id`,`device_id`,`description`,`task_at`,`is_completed`,`audit_status`) VALUES (@user,@device,@description,UTC_TIMESTAMP(3),0,0);";
            await command.ExecuteNonQueryAsync(cancellationToken);
            long id = command.LastInsertedId;
            command.Parameters.AddWithValue("@id", id);
            command.CommandText = "UPDATE `checklist_work_order_sources` SET `work_order_id`=@id WHERE `source_key`=@key;";
            await command.ExecuteNonQueryAsync(cancellationToken);
            inserted++;
        }
        await transaction.CommitAsync(cancellationToken);
        return new(inserted, request.Items.Count - inserted, userId, "my9281");
    }
}
