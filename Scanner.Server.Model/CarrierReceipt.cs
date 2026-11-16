namespace Scanner.Server.Model;

public sealed record CarrierReceiptScan(Guid ScanId, Guid DeviceId, Guid BatchId, string BatchNumber,
    int DomainId, ulong OperatorUserId, string SiteId, string Carrier, string TrackingNumber, string RawBarcode, DateTimeOffset ScannedAt);
public sealed record CarrierReceiptBatch(IReadOnlyList<CarrierReceiptScan> Items);
public sealed record CarrierReceiptAcknowledgement(IReadOnlyList<Guid> AcceptedScanIds, int NewlyReceivedCount);
public sealed record CarrierReceiptContext(int DomainId, ulong UserId, string Username, string SiteId, string Server = "");

public static class CarrierReceiptValidation
{
    public static string NormalizeNumber(string raw)
    {
        string value = raw.Trim();
        if (value.Length is < 8 or > 40 || value.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("请扫描 8–40 位普通运单号；组合条码请改扫面单上的运单条码。");
        return value.ToUpperInvariant();
    }

    public static void Validate(CarrierReceiptScan scan, DateTimeOffset now)
    {
        if (scan.ScanId == Guid.Empty || scan.DeviceId == Guid.Empty || scan.BatchId == Guid.Empty || scan.OperatorUserId == 0 || scan.DomainId <= 0)
            throw new ArgumentException("扫描 ID、设备、批次或操作人无效。");
        if (scan.Carrier is not ("fedex" or "usps")) throw new ArgumentException("请选择 FedEx 或 USPS。");
        if (string.IsNullOrWhiteSpace(scan.SiteId) || scan.SiteId.Length > 100 || scan.SiteId.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(scan.BatchNumber) || scan.BatchNumber.Length > 100 || scan.BatchNumber.Any(char.IsControl))
            throw new ArgumentException("站点和收货批次必须为 1–100 个有效字符。");
        if (scan.RawBarcode is null || scan.RawBarcode.Length > 4096 || scan.TrackingNumber != NormalizeNumber(scan.RawBarcode))
            throw new ArgumentException("运单号与原始扫码内容不一致。");
        if (scan.ScannedAt == default || scan.ScannedAt < DateTimeOffset.UnixEpoch || scan.ScannedAt > now.AddMinutes(5))
            throw new ArgumentException("扫码时间无效，请检查 PDA 时间。");
    }
}
