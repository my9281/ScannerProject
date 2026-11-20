namespace Scanner.Server.Model;

public sealed record CarrierReceiptScan(Guid ScanId, Guid DeviceId, Guid BatchId, string BatchNumber,
    int DomainId, ulong OperatorUserId, string SiteId, string Carrier, string TrackingNumber, string RawBarcode, DateTimeOffset ScannedAt);
public sealed record CarrierReceiptBatch(IReadOnlyList<CarrierReceiptScan> Items);
public sealed record CarrierReceiptAcknowledgement(IReadOnlyList<Guid> AcceptedScanIds, int NewlyReceivedCount);
public sealed record CarrierReceiptContext(int DomainId, ulong UserId, string Username, string SiteId, string Server = "");

public static class CarrierReceiptValidation
{
    // Scanner group/record separators are transport characters, not part of the tracking number.
    // Keep RawBarcode untouched for auditing.
    private static string ScannerValue(string raw) => new string(raw.Where(c => c is not ('\u001c' or '\u001d' or '\u001e' or '\u001f' or '\u0004' or '\t')).ToArray()).Trim();
    private static string? DetectCarrier(string value)
    {
        if (value.StartsWith("TBA", StringComparison.OrdinalIgnoreCase) && value[3..].All(char.IsAsciiDigit))
            return "tba";
        if (value.StartsWith("[)>", StringComparison.Ordinal)) return "fedex";
        if (value.Length == 13 && value.EndsWith("US", StringComparison.OrdinalIgnoreCase)
            && char.IsAsciiLetter(value[0]) && char.IsAsciiLetter(value[1]) && value[2..11].All(char.IsAsciiDigit))
            return "usps";
        if (!value.All(char.IsAsciiDigit)) return null;
        // The warehouse identifies these complete 420 routing scans as FedEx.
        if (value.StartsWith("420", StringComparison.Ordinal) && value.Length is 30 or 34) return "fedex";
        if (value.Length is 20 or 22 && value[0] == '9' && value[1] is >= '1' and <= '5') return "usps";
        if (value.Length is 12 or 15 or 20
            || value.Length is 22 or 27 or 34 && value.StartsWith("96", StringComparison.Ordinal)) return "fedex";
        return null;
    }
    public static string CarrierName(string carrier) => carrier switch { "fedex" => "FedEx", "usps" => "USPS", "tba" => "TBA", _ => carrier };
    public static string NormalizeNumber(string raw)
    {
        if (raw is null || raw.Length > 4096) throw new ArgumentException("扫码内容无效。");
        string value = ScannerValue(raw);
        if (value.StartsWith("[)>", StringComparison.Ordinal))
        {
            // The 31Z field carries the 34-digit barcode: strip its seven routing digits.
            var numbers = System.Text.RegularExpressions.Regex.Matches(value, @"31Z96[0-9]{5}(96[0-9]{25})(?=32Z|[\x1d\x1e]|$)")
                .Select(m => m.Groups[1].Value).Distinct(StringComparer.Ordinal).ToArray();
            if (numbers.Length == 1) return numbers[0];
            throw new ArgumentException("无法识别组合条码中的运单号，请扫描运单条码。");
        }
        if (value.Length == 34 && value.StartsWith("96", StringComparison.Ordinal) && value.All(char.IsAsciiDigit))
            return value[7..];
        if (value.Length is < 8 or > 40 || value.Any(c => !char.IsAsciiLetterOrDigit(c)))
            throw new ArgumentException("请扫描有效运单号。");
        return value.ToUpperInvariant();
    }

    public static string? ScanWarning(string raw, out string? carrier, out string number)
    {
        carrier = null;
        number = raw;
        if (raw.Length > 4096) return "不是运单条码，请重扫。";
        string value = ScannerValue(raw);
        if (value.StartsWith("420", StringComparison.Ordinal) && value.Length is 8 or 12 && value.All(char.IsAsciiDigit))
            return "地区码，请扫描运单条码。";
        try { number = NormalizeNumber(raw); }
        catch (ArgumentException) { return "不是运单条码，请重扫。"; }
        carrier = DetectCarrier(value);
        return carrier is null ? "无法识别承运商，请扫描运单条码。" : null;
    }

    public static void Validate(CarrierReceiptScan scan, DateTimeOffset now)
    {
        if (scan.ScanId == Guid.Empty || scan.DeviceId == Guid.Empty || scan.BatchId == Guid.Empty || scan.OperatorUserId == 0 || scan.DomainId <= 0)
            throw new ArgumentException("扫描 ID、设备、批次或操作人无效。");
        if (scan.Carrier is not ("fedex" or "usps" or "tba")) throw new ArgumentException("请选择 FedEx、USPS 或 TBA。");
        if (string.IsNullOrWhiteSpace(scan.SiteId) || scan.SiteId.Length > 100 || scan.SiteId.Any(char.IsControl)
            || string.IsNullOrWhiteSpace(scan.BatchNumber) || scan.BatchNumber.Length > 100 || scan.BatchNumber.Any(char.IsControl))
            throw new ArgumentException("站点和收货批次必须为 1–100 个有效字符。");
        if (scan.RawBarcode is null || scan.RawBarcode.Length > 4096)
            throw new ArgumentException("运单号与原始扫码内容不一致。");
        string normalized = NormalizeNumber(scan.RawBarcode);
        // Preserve replay of existing outbox entries created by the older 12-digit parser.
        bool legacy = scan.RawBarcode.TrimStart().StartsWith("[)>", StringComparison.Ordinal)
            && System.Text.RegularExpressions.Regex.Matches(scan.RawBarcode, @"([0-9]{12})FDE(?:G|X)")
                .Any(m => m.Groups[1].Value == scan.TrackingNumber);
        if (scan.TrackingNumber != normalized && !legacy) throw new ArgumentException("运单号与原始扫码内容不一致。");
        if (scan.ScannedAt == default || scan.ScannedAt < DateTimeOffset.UnixEpoch || scan.ScannedAt > now.AddMinutes(5))
            throw new ArgumentException("扫码时间无效，请检查 PDA 时间。");
    }
}
