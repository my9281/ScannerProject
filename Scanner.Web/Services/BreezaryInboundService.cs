using Scanner.Server.Model;

namespace Scanner.Web.Services;

public interface IBreezaryInboundStore
{
    Task SaveAsync(IReadOnlyList<BreezaryInboundItem> items, CancellationToken ct);
    Task<BreezaryInboundPage> ListAsync(ulong beforeId, int limit, int? location, string? search, CancellationToken ct);
}

public sealed class BreezaryInboundService(IBreezaryInboundStore store)
{
    public async Task<BreezaryInboundAcknowledgement> UploadAsync(BreezaryInboundBatch batch, CancellationToken ct)
    {
        BreezaryInboundValidation.Validate(batch.Items);
        await store.SaveAsync(batch.Items, ct);
        return new(batch.Items.Select(x => x.ScanId).ToArray(), DateTimeOffset.UtcNow);
    }

    public Task<BreezaryInboundPage> ListAsync(ulong beforeId, int limit, int? location, string? search, CancellationToken ct)
    {
        if (limit is < 1 or > 200 || location is < 1 or > 100 || search?.Length > 200)
            throw new ArgumentException("每页为 1–200 条，位置为 1–100，搜索词最多 200 字符。");
        return store.ListAsync(beforeId, limit, location, search, ct);
    }
}
