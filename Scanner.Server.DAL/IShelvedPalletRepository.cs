using Scanner.Server.Model;

namespace Scanner.Server.DAL;

public interface IShelvedPalletRepository
{
    Task<IReadOnlyList<ShelvedPalletRecord>> CreateBatchAsync(
        string palletNumber,
        DateTime shelvedAt,
        IReadOnlyList<ShelvedPalletItemRequest> items,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShelvedPalletRecord>> QueryAsync(
        DateTime? from,
        DateTime? to,
        string? palletNumber,
        int limit,
        CancellationToken cancellationToken = default);

    Task<ShelvedPalletTableResult> GetAllAsync(CancellationToken cancellationToken = default);
}
