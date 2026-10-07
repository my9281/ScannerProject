using Scanner.Server.Model;
namespace Scanner.Server.DAL;
public interface IPalletScanRepository
{
    Task<IReadOnlyList<PalletScanStoredRow>> GetAllAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyList<PalletScanItem> items, CancellationToken cancellationToken = default);
}
