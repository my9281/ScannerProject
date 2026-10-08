using Scanner.Server.DAL;
using Scanner.Server.Model;
namespace Scanner.Server.BLL;

public interface IPalletScanService
{
    Task<IReadOnlyList<PalletScanStoredRow>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<PalletScanBatchResult> UploadAsync(PalletScanBatchRequest request, CancellationToken cancellationToken = default);
}
public sealed class PalletScanService(IPalletScanRepository repository) : IPalletScanService
{
    public Task<IReadOnlyList<PalletScanStoredRow>> GetAllAsync(CancellationToken cancellationToken = default) => repository.GetAllAsync(cancellationToken);
    public async Task<PalletScanBatchResult> UploadAsync(PalletScanBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Items is null || request.Items.Count is < 1 or > 1000)
            throw new ArgumentException("每批必须包含 1–1000 条扫描记录。");
        var items = new List<PalletScanItem>();
        var ids = new HashSet<Guid>();
        foreach (var item in request.Items)
        {
            if (item is null || item.ScanId == Guid.Empty || !ids.Add(item.ScanId)) throw new ArgumentException("扫描标识不能为空或重复。");
            var sn = item.Sn?.Trim();
            if (string.IsNullOrEmpty(sn) || sn.Length > 100) throw new ArgumentException("SN 必须为 1–100 个字符。");
            if (item.PalletNumber is < 1 or > 100) throw new ArgumentException("托盘号必须为 1–100。");
            if (item.ScannedAt.Year < 1000) throw new ArgumentException("扫描时间无效。");
            // Match the storage precision of MySQL DATETIME(3).
            var time = item.ScannedAt.AddTicks(-(item.ScannedAt.Ticks % TimeSpan.TicksPerMillisecond));
            items.Add(item with { Sn = sn, ScannedAt = time });
        }
        await repository.SaveAsync(items, cancellationToken);
        return new PalletScanBatchResult(items.Select(x => x.ScanId).ToArray());
    }
}
