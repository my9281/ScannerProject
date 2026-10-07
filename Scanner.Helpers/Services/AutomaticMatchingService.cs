using Scanner.Models;
using System.Globalization;

namespace Scanner.Helpers.Services
{
    public sealed class PdaPalletRow
    {
        public string Sn { get; set; }
        public int PalletNumber { get; set; }
        public DateTime ScanDate { get; set; }
        public DateTime ScanTime { get; set; }
    }
    public sealed class AutomaticMatchRow
    {
        public int PalletNumber { get; set; }
        public string Sn { get; set; }
        public string Sku { get; set; }
        public string RmaNumber { get; set; }
        public DateTime ScanDate { get; set; }
        public DateTime ScanTime { get; set; }
        public string Type { get; set; }
        public string DetectionStatus { get; set; }
        public string ProcessingTime { get; set; }
    }
    public static class AutomaticMatchingService
    {
        public static IReadOnlyList<AutomaticMatchRow> Match(IEnumerable<PdaPalletRow> pallets, IEnumerable<InboundChecklistRecord> inbound)
        {
            var pending = inbound.Where(x => !string.IsNullOrWhiteSpace(x.Sn) && (x.DetectionStatus ?? "").Trim() == "待检测")
                .ToLookup(x => x.Sn.Trim(), StringComparer.OrdinalIgnoreCase);
            return pallets.Where(x => !string.IsNullOrWhiteSpace(x.Sn))
                .OrderBy(x => x.PalletNumber).ThenBy(x => x.ScanTime).ThenBy(x => x.Sn, StringComparer.OrdinalIgnoreCase)
                .SelectMany(p => pending[p.Sn.Trim()].Select(b => new AutomaticMatchRow {
                    PalletNumber = p.PalletNumber, Sn = b.Sn.Trim(), Sku = b.Sku, RmaNumber = b.RmaNumber,
                    ScanDate = p.ScanDate, ScanTime = p.ScanTime, Type = b.Type,
                    DetectionStatus = b.DetectionStatus, ProcessingTime = b.ProcessingTime
                })).ToList().AsReadOnly();
        }
        public static void Export(string path, IReadOnlyList<AutomaticMatchRow> rows)
        {
            var table = new List<object[]> { new object[] { "托盘号", "SN", "SKU", "RMA编号", "扫描日期", "扫描时间", "类型", "检测状态", "处理时间" } };
            table.AddRange(rows.Select(x => new object[] { x.PalletNumber, x.Sn, x.Sku, x.RmaNumber,
                x.ScanDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), x.ScanTime.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                x.Type, x.DetectionStatus, x.ProcessingTime }));
            WarehouseRentService.WriteTables(path, new[] { "自动匹配" }, new[] { table });
        }
    }
}
