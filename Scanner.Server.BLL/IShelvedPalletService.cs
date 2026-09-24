using Scanner.Server.Model;

namespace Scanner.Server.BLL;

public interface IShelvedPalletService
{
    Task<ShelvedPalletTableResult> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CreateShelvedPalletBatchResult> CreateBatchAsync(CreateShelvedPalletBatchRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShelvedPalletRecord>> QueryAsync(DateTime? from, DateTime? to, string? palletNumber, int limit, CancellationToken cancellationToken = default);
}
