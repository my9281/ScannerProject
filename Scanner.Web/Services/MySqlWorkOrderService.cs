using MySqlConnector;
using Scanner.Server.DAL;

namespace Scanner.Web.Services;

public sealed record WorkOrderResult(ulong Id, ulong UserId, string DeviceId, string Description,
    bool IsCompleted, DateTimeOffset TaskAt, DateTimeOffset? CompletedAt, DateTimeOffset UpdatedAt, uint Version);

public interface IWorkOrderService
{
    Task<IReadOnlyList<WorkOrderResult>> DownloadAsync(ulong userId, ulong afterId, CancellationToken cancellationToken);
    Task<WorkOrderResult?> UpdateAsync(ulong userId, ulong id, bool completed, uint version, CancellationToken cancellationToken);
}

public sealed class MySqlWorkOrderService(IMySqlConnectionFactory connections) : IWorkOrderService
{
    private const string Columns = "`id`,`user_id`,`device_id`,`description`,`is_completed`,`task_at`,`completed_at`,`updated_at`,`version`";
    public async Task<IReadOnlyList<WorkOrderResult>> DownloadAsync(ulong userId, ulong afterId, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {Columns} FROM `checklist_work_orders` WHERE `user_id`=@userId AND `audit_status`=0 AND `id`>@afterId ORDER BY `id` LIMIT 200;";
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@afterId", afterId);
        var rows = new List<WorkOrderResult>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) rows.Add(Read(reader));
        return rows;
    }
    public async Task<WorkOrderResult?> UpdateAsync(ulong userId, ulong id, bool completed, uint version, CancellationToken cancellationToken)
    {
        await using var connection = await connections.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            UPDATE `checklist_work_orders`
            SET `is_completed`=@completed,
                `completed_at`=CASE WHEN @completed=0 THEN NULL WHEN `completed_at` IS NULL THEN UTC_TIMESTAMP(3) ELSE `completed_at` END,
                `updated_at`=UTC_TIMESTAMP(3), `version`=`version`+1
            WHERE `id`=@id AND `user_id`=@userId AND `audit_status`=0 AND `version`=@version;
            """;
        command.Parameters.AddWithValue("@completed", completed ? 1 : 0);
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@version", version);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1) return null;
        command.CommandText = $"SELECT {Columns} FROM `checklist_work_orders` WHERE `id`=@id AND `user_id`=@userId AND `audit_status`=0;";
        WorkOrderResult result;
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            if (!await reader.ReadAsync(cancellationToken)) return null;
            result = Read(reader);
        }
        await transaction.CommitAsync(cancellationToken);
        return result;
    }
    private static DateTimeOffset Utc(DateTime value) => new(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    private static WorkOrderResult Read(MySqlDataReader reader) => new(
        reader.GetUInt64("id"), reader.GetUInt64("user_id"), reader.GetString("device_id"), reader.GetString("description"),
        reader.GetByte("is_completed") == 1, Utc(reader.GetDateTime("task_at")),
        reader.IsDBNull(reader.GetOrdinal("completed_at")) ? null : Utc(reader.GetDateTime("completed_at")),
        Utc(reader.GetDateTime("updated_at")), reader.GetUInt32("version"));
}
