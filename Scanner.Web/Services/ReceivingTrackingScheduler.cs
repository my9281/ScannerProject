using Scanner.Server.DAL;

namespace Scanner.Web.Services;

public sealed class ReceivingTrackingScheduler(IMySqlConnectionFactory connections, CarrierReceivingStore carriers, CarrierTrackingClient tracking)
{
    public async Task Run(int domain, CancellationToken ct)
    {
        await using var c = await connections.OpenConnectionAsync(ct);
        string key = "tracking:" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(c.Database + ":" + domain)))[..48];
        if (!await ReceivingSyncStore.Lock(c, key, ct)) return;
        try
        {
            using var cmd = c.CreateCommand(); cmd.Parameters.AddWithValue("@domain", domain);
            // Only known, unreceived packages. Fairly ordered by last attempt, including failures.
            cmd.CommandText = """
                SELECT p.id,p.carrier,p.tracking_number FROM carrier_receiving p
                JOIN receiving_sync_settings s ON s.domain_id=p.domain_id AND s.auto_track=1
                JOIN carrier_settings a ON a.domain_id=p.domain_id AND a.carrier=p.carrier AND a.enabled=1
                  AND (a.retry_after_at IS NULL OR a.retry_after_at<=UTC_TIMESTAMP(3))
                WHERE p.domain_id=@domain AND p.received_at IS NULL
                AND (p.last_attempt_at IS NULL OR p.last_attempt_at<=DATE_SUB(UTC_TIMESTAMP(3),INTERVAL s.track_interval_seconds SECOND))
                ORDER BY p.last_attempt_at,p.id LIMIT 20
                """;
            var items = new List<(ulong Id, string Carrier, string Number)>();
            await using (var r = await cmd.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) items.Add((r.GetUInt64(0), r.GetString(1), r.GetString(2)));
            foreach (var item in items)
            {
                cmd.Parameters.Clear(); cmd.Parameters.AddWithValue("@id", item.Id); cmd.Parameters.AddWithValue("@domain", domain);
                cmd.CommandText = "UPDATE carrier_receiving SET last_attempt_at=UTC_TIMESTAMP(3) WHERE id=@id AND domain_id=@domain AND received_at IS NULL";
                await cmd.ExecuteNonQueryAsync(ct);
                try
                {
                    var credentials = await carriers.Credentials(domain, item.Carrier, ct);
                    var result = await tracking.TrackAsync(item.Carrier, item.Number, credentials, ct);
                    await carriers.UpdateTracking(domain, item.Carrier, item.Number, result, ct);
                    cmd.CommandText = "UPDATE carrier_receiving SET last_error='' WHERE id=@id AND domain_id=@domain";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                catch (Exception ex) when (!ct.IsCancellationRequested)
                {
                    string message = ex is ApiException ? ex.Message : "物流自动查询失败。";
                    cmd.Parameters.AddWithValue("@error", message.Length > 500 ? message[..500] : message);
                    cmd.CommandText = "UPDATE carrier_receiving SET last_error=@error WHERE id=@id AND domain_id=@domain";
                    await cmd.ExecuteNonQueryAsync(ct);
                    cmd.Parameters.AddWithValue("@carrier", item.Carrier);
                    cmd.CommandText = """
                        UPDATE carrier_settings a JOIN receiving_sync_settings s ON s.domain_id=a.domain_id
                        SET a.retry_after_at=DATE_ADD(UTC_TIMESTAMP(3),INTERVAL s.track_interval_seconds SECOND)
                        WHERE a.domain_id=@domain AND a.carrier=@carrier
                        """;
                    await cmd.ExecuteNonQueryAsync(ct);
                    // Stop this batch on provider error instead of hammering credentials or an exhausted quota.
                    break;
                }
            }
        }
        finally { await ReceivingSyncStore.Unlock(c, key); }
    }
}
