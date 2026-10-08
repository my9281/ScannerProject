using System.Globalization;
using Scanner.Server.Model;
namespace Scanner.Server.BLL;
public static class SkuSnBatchValidator
{
    public static SkuSnBatch Validate(SkuSnBatch batch)
    {
        if (batch is null || batch.BatchId == Guid.Empty || batch.Items is null || batch.Items.Count is < 1 or > 5000) throw new ArgumentException("批次须包含 GUID 和 1–5000 条记录。");
        if (batch.CreatedAt.UtcDateTime.Year < 1000 || batch.BatchNumber != batch.CreatedAt.ToString("yyyyMMddmmss", CultureInfo.InvariantCulture)) throw new ArgumentException("批次号与批次时间不匹配。");
        var sns = new HashSet<string>(StringComparer.Ordinal);
        var items = batch.Items.Select(x => {
            var sku = x?.Sku?.Trim(); var sn = x?.Sn?.Trim();
            if (string.IsNullOrEmpty(sku) || sku.Length > 150 || sku.Any(char.IsControl) || string.IsNullOrEmpty(sn) || sn.Length > 100 || sn.Any(char.IsControl)) throw new ArgumentException("SKU 或 SN 无效。");
            if (!sns.Add(sn)) throw new ArgumentException("批次内 SN 不能重复。");
            return new SkuSnItem(sku, sn);
        }).ToArray();
        return batch with { Items = items, CreatedAt = batch.CreatedAt.AddTicks(-(batch.CreatedAt.Ticks % TimeSpan.TicksPerMillisecond)) };
    }
}
