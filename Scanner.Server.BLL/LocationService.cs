using Scanner.Server.DAL;
using Scanner.Server.Model;
namespace Scanner.Server.BLL;
public interface ILocationService
{
    Task<LocationBatchResult> UploadAsync(LocationBatchRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocationRow>> GetAllAsync(CancellationToken cancellationToken = default);
}
public sealed class LocationService(ILocationRepository repository) : ILocationService
{
    public Task<IReadOnlyList<LocationRow>> GetAllAsync(CancellationToken cancellationToken = default) => repository.GetAllAsync(cancellationToken);
    public async Task<LocationBatchResult> UploadAsync(LocationBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Items is null || request.Items.Count is < 1 or > 1000) throw new ArgumentException("每批必须包含 1–1000 条库位记录。");
        var items = new List<LocationScanItem>();
        foreach (var item in request.Items)
        {
            var id = item?.LocationId?.Trim();
            if (string.IsNullOrEmpty(id) || id.Length > 100 || id.Any(char.IsControl)) throw new ArgumentException("库位 ID 必须为 1–100 个字符，且不能包含控制字符。");
            if (item!.ScannedAt.UtcDateTime.Year < 1000 || item.ScannedAt.UtcDateTime.Year > 9999) throw new ArgumentException("扫描时间无效。");
            items.Add(item with { LocationId = id });
        }
        var unique = items.DistinctBy(x => x.LocationId, StringComparer.Ordinal).ToArray();
        await repository.SaveAsync(unique, cancellationToken);
        return new(unique.Select(x => x.LocationId).ToArray());
    }
}
