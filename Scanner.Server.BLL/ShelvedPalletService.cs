using Scanner.Server.DAL;
using Scanner.Server.Model;

namespace Scanner.Server.BLL;

public sealed class ShelvedPalletService(IShelvedPalletRepository repository) : IShelvedPalletService
{
    public Task<ShelvedPalletTableResult> GetAllAsync(CancellationToken cancellationToken = default)
        => repository.GetAllAsync(cancellationToken);

    public async Task<CreateShelvedPalletBatchResult> CreateBatchAsync(CreateShelvedPalletBatchRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null) throw new ArgumentException("上传内容不能为空。");
        string palletNumber = Required(request.PalletNumber, "托盘号", 100);
        if (request.Items is null || request.Items.Count == 0) throw new ArgumentException("出库明细不能为空。");
        if (request.Items.Count > 5000) throw new ArgumentException("单次上传不能超过 5000 条。");

        ShelvedPalletItemRequest[] items = request.Items.Select(item => new ShelvedPalletItemRequest(
            item.Number > 0 ? item.Number : throw new ArgumentException("出库编号必须大于 0。"),
            Required(item.Sn, "SN", 100),
            Required(item.Sku, "SKU", 150),
            Optional(item.Type, 100, "类型"),
            Optional(item.ProcessingMethod, 100, "处理方式"),
            Optional(item.ProcessingTime, 50, "处理时间"))).ToArray();

        DateTime shelvedAt = DateTime.Now;
        IReadOnlyList<ShelvedPalletRecord> created = await repository.CreateBatchAsync(palletNumber, shelvedAt, items, cancellationToken);
        return new CreateShelvedPalletBatchResult(palletNumber, shelvedAt, created.Count, created.Select(item => item.Uuid).ToArray());
    }

    public Task<IReadOnlyList<ShelvedPalletRecord>> QueryAsync(DateTime? from, DateTime? to, string? palletNumber, int limit, CancellationToken cancellationToken = default)
    {
        if (from.HasValue && to.HasValue && from > to) throw new ArgumentException("开始时间不能晚于结束时间。");
        if (palletNumber?.Trim().Length > 100) throw new ArgumentException("托盘号不能超过 100 个字符。");
        if (limit is < 1 or > 1000) throw new ArgumentException("limit 必须在 1 到 1000 之间。");
        return repository.QueryAsync(from, to, string.IsNullOrWhiteSpace(palletNumber) ? null : palletNumber.Trim(), limit, cancellationToken);
    }

    private static string Required(string? value, string name, int maxLength)
    {
        string result = value?.Trim() ?? string.Empty;
        if (result.Length == 0) throw new ArgumentException($"{name}不能为空。");
        if (result.Length > maxLength) throw new ArgumentException($"{name}不能超过 {maxLength} 个字符。");
        return result;
    }

    private static string? Optional(string? value, int maxLength, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        string result = value.Trim();
        if (result.Length > maxLength) throw new ArgumentException($"{name}不能超过 {maxLength} 个字符。");
        return result;
    }
}
