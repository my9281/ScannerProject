using MySqlConnector;
using Scanner.Server.DAL;
using Scanner.Server.Model;

namespace Scanner.Web.Services;

public sealed class CarrierReceiptService(IMySqlConnectionFactory connections)
{
    public async Task<object> List(int domain, int page, string search, bool board, CancellationToken ct)
    {
        if (page is < 0 or > 10000 || search.Length > 200) throw new ArgumentException("分页或搜索条件无效。");
        await using var c = await connections.OpenConnectionAsync(ct); using var cmd = c.CreateCommand();
        cmd.CommandText = board ? """
            SELECT s.scan_id,s.site_id,s.batch_number,s.carrier,s.tracking_number,s.operator_username,s.scanned_at,s.uploaded_at,
                   CASE WHEN d.id=@domain THEN
                     (SELECT f.rma FROM bluetti_receiving_forecasts f
                      WHERE f.domain_id=d.id AND f.carrier=s.carrier AND f.tracking_number=s.tracking_number AND f.issue=''
                      ORDER BY f.last_seen_at DESC,f.id DESC LIMIT 1)
                   ELSE NULL END AS rma
            FROM carrier_receipt_scans s
            LEFT JOIN user_domains d ON d.domain_name=@bluetti AND d.is_enabled=1
            WHERE (UPPER(s.operator_username)='PDA01' OR s.domain_id=@domain)
              AND ((d.id=@domain AND EXISTS(
                     SELECT 1 FROM bluetti_receiving_forecasts f WHERE f.domain_id=d.id
                       AND f.carrier=s.carrier AND f.tracking_number=s.tracking_number AND f.issue=''))
                OR (COALESCE(d.id,0)<>@domain AND NOT EXISTS(
                     SELECT 1 FROM bluetti_receiving_forecasts f WHERE f.domain_id=d.id
                       AND f.carrier=s.carrier AND f.tracking_number=s.tracking_number AND f.issue='')))
            """ : """
            SELECT s.scan_id,s.site_id,s.batch_number,s.carrier,s.tracking_number,s.operator_username,s.scanned_at,s.uploaded_at,NULL AS rma
            FROM carrier_receipt_scans s
            WHERE UPPER(s.operator_username)='PDA01'
            """;
        cmd.CommandText += """

              AND (@search='' OR LOCATE(@search,s.tracking_number)>0 OR LOCATE(@search,s.batch_number)>0)
            ORDER BY s.uploaded_at DESC,s.scan_id DESC LIMIT 100 OFFSET @offset
            """;
        if (board)
        {
            cmd.Parameters.AddWithValue("@domain", domain);
            cmd.Parameters.AddWithValue("@bluetti", ReceivingSyncStore.BluettiDomainName);
        }
        cmd.Parameters.AddWithValue("@search", search); cmd.Parameters.AddWithValue("@offset", page * 100);
        await using var r = await cmd.ExecuteReaderAsync(ct); var rows = new List<object>();
        while (await r.ReadAsync(ct)) rows.Add(new { scanId = r.GetGuid(0), siteId = r.GetString(1), batchNumber = r.GetString(2), carrier = r.GetString(3), trackingNumber = r.GetString(4),
            receivedBy = r.GetString(5), scannedAt = DateTime.SpecifyKind(r.GetDateTime(6), DateTimeKind.Utc), uploadedAt = DateTime.SpecifyKind(r.GetDateTime(7), DateTimeKind.Utc),
            rma = r.IsDBNull(8) ? null : r.GetString(8) });
        return rows;
    }
    // P0 receipt belongs to the authenticated WMS domain, independently of customer forecast configuration.
    public Task<CarrierReceiptContext> Context(AccountResult account, CancellationToken ct) =>
        Task.FromResult(new CarrierReceiptContext(account.Domain.Id, account.UserId, account.Username, "domain-" + account.Domain.Id));

    public static void ValidateBatch(CarrierReceiptBatch batch, AccountResult account)
    {
        if (batch.Items is null || batch.Items.Count is < 1 or > 100) throw new ArgumentException("每次上传 1–100 条收货扫描。");
        if (batch.Items.Any(x => x is null) || batch.Items.Select(x => x.ScanId).Distinct().Count() != batch.Items.Count)
            throw new ArgumentException("扫描记录为空或扫描 ID 重复。");
        foreach (var scan in batch.Items)
        {
            CarrierReceiptValidation.Validate(scan, DateTimeOffset.UtcNow);
            if (scan.DomainId != account.Domain.Id || scan.OperatorUserId != account.UserId)
                throw new ApiException(403, "待上传记录属于其他账号或域，请使用原收货账号登录。");
        }
    }

    public static CarrierReceiptBatch AttributePdaReceipt(CarrierReceiptBatch batch, AccountResult account) =>
        account.Username.Equals("PDA01", StringComparison.OrdinalIgnoreCase) && batch.Items is not null
            ? new(batch.Items.Select(s => s is not null && (s.DomainId == 0 || s.DomainId == account.Domain.Id)
                ? s with { DomainId = account.Domain.Id, OperatorUserId = account.UserId } : s!).ToArray())
            : batch;

    public async Task<CarrierReceiptAcknowledgement> Upload(CarrierReceiptBatch batch, AccountResult account, CancellationToken ct)
    {
        batch = AttributePdaReceipt(batch, account);
        ValidateBatch(batch, account);
        await using var c = await connections.OpenConnectionAsync(ct); await using var tx = await c.BeginTransactionAsync(ct);
        await AccountManagementService.Revalidate(c, tx, account, false, ct);
        int newlyReceived = 0;
        foreach (var scan in batch.Items.OrderBy(s => s.ScanId))
        {
            using var insert = c.CreateCommand(); insert.Transaction = tx;
            insert.Parameters.AddWithValue("@id", scan.ScanId.ToString("D")); insert.Parameters.AddWithValue("@domain", account.Domain.Id);
            insert.Parameters.AddWithValue("@userId", account.UserId); insert.Parameters.AddWithValue("@user", account.Username);
            insert.Parameters.AddWithValue("@device", scan.DeviceId.ToString("D")); insert.Parameters.AddWithValue("@batchId", scan.BatchId.ToString("D"));
            insert.Parameters.AddWithValue("@batch", scan.BatchNumber); insert.Parameters.AddWithValue("@site", scan.SiteId);
            insert.Parameters.AddWithValue("@carrier", scan.Carrier); insert.Parameters.AddWithValue("@number", scan.TrackingNumber);
            insert.Parameters.AddWithValue("@raw", scan.RawBarcode);
            insert.Parameters.AddWithValue("@scanned", DateTimeOffset.FromUnixTimeMilliseconds(scan.ScannedAt.ToUnixTimeMilliseconds()).UtcDateTime);
            // Already committed retries are acknowledged even if the configured site was subsequently changed.
            insert.CommandText = "SELECT domain_id,operator_user_id,device_id,batch_id,batch_number,site_id,carrier,tracking_number,raw_barcode,scanned_at FROM carrier_receipt_scans WHERE scan_id=@id FOR UPDATE";
            bool exists;
            await using (var r = await insert.ExecuteReaderAsync(ct))
            {
                exists = await r.ReadAsync(ct);
                if (exists && (r.GetInt32(0) != account.Domain.Id || r.GetUInt64(1) != account.UserId || r.GetGuid(2) != scan.DeviceId || r.GetGuid(3) != scan.BatchId
                    || r.GetString(4) != scan.BatchNumber || r.GetString(5) != scan.SiteId || r.GetString(6) != scan.Carrier || r.GetString(7) != scan.TrackingNumber
                    || r.GetString(8) != scan.RawBarcode || r.GetDateTime(9) != DateTimeOffset.FromUnixTimeMilliseconds(scan.ScannedAt.ToUnixTimeMilliseconds()).UtcDateTime))
                    throw new ApiException(409, "扫描 ID 已对应其他内容，整批未确认。");
            }
            if (exists) continue;
            // Domain/user ownership is validated above. SiteId is retained as scan metadata, including older offline records.
            // Neither a customer forecast nor the currently configured BLUETTI site is a prerequisite for receipt.
            insert.CommandText = """
                INSERT INTO carrier_receipt_scans(scan_id,domain_id,operator_user_id,operator_username,device_id,batch_id,batch_number,site_id,carrier,tracking_number,raw_barcode,scanned_at,uploaded_at)
                VALUES(@id,@domain,@userId,@user,@device,@batchId,@batch,@site,@carrier,@number,@raw,@scanned,UTC_TIMESTAMP(3))
                """;
            await insert.ExecuteNonQueryAsync(ct);
            insert.CommandText = """
                INSERT INTO carrier_receiving(domain_id,carrier,tracking_number,reference,created_at,created_by)
                VALUES(@domain,@carrier,@number,'',UTC_TIMESTAMP(3),@user)
                ON DUPLICATE KEY UPDATE tracking_number=tracking_number
                """;
            await insert.ExecuteNonQueryAsync(ct);
            // Do not overwrite a confirmed first receipt on subsequent scans or offline retries.
            insert.CommandText = "UPDATE carrier_receiving SET received_at=@scanned,received_by=@user WHERE domain_id=@domain AND carrier=@carrier AND tracking_number=@number AND received_at IS NULL";
            newlyReceived += await insert.ExecuteNonQueryAsync(ct);
        }
        await tx.CommitAsync(ct);
        return new(batch.Items.Select(s => s.ScanId).ToArray(), newlyReceived);
    }
}
