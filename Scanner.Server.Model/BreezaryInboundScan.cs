namespace Scanner.Server.Model;

public sealed record BreezaryInboundItem(Guid ScanId, Guid DeviceId, int CacheLocation, string Barcode, DateTimeOffset ScannedAt);
public sealed record BreezaryInboundBatch(IReadOnlyList<BreezaryInboundItem> Items);
public sealed record BreezaryInboundAcknowledgement(IReadOnlyList<Guid> AcknowledgedIds, DateTimeOffset AcknowledgedAt);
public sealed record BreezaryInboundStored(ulong Id, Guid ScanId, Guid DeviceId, int CacheLocation, string Barcode,
    DateTimeOffset ScannedAt, DateTimeOffset UploadedAt);
public sealed record BreezaryInboundPage(IReadOnlyList<BreezaryInboundStored> Records, bool HasMore, ulong NextId);

public static class BreezaryInboundValidation
{
    public static void Validate(IReadOnlyList<BreezaryInboundItem>? items)
    {
        if (items is null || items.Count is < 1 or > 200) throw new ArgumentException("每批上传必须为 1–200 条。");
        var ids = new HashSet<Guid>();
        foreach (var item in items)
        {
            if (item is null || item.ScanId == Guid.Empty || item.DeviceId == Guid.Empty || !ids.Add(item.ScanId))
                throw new ArgumentException("扫描记录或设备 ID 无效，或批次内存在重复记录 ID。");
            if (item.CacheLocation is < 1 or > 100) throw new ArgumentException("缓存位置必须为 1–100。");
            if (string.IsNullOrWhiteSpace(item.Barcode) || item.Barcode.Length > 4096) throw new ArgumentException("扫描内容不能为空，且不能超过 4096 字符。");
            if (item.ScannedAt == default || item.ScannedAt.Year is < 2000 or > 9998) throw new ArgumentException("扫描时间无效，请检查设备时间。");
        }
    }
}
