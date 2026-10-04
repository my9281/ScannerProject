using Scanner.Server.Model;
namespace Scanner.Server.DAL;
public interface IPalletScanRepository
{
    Task SaveAsync(IReadOnlyList<PalletScanItem> items, CancellationToken cancellationToken = default);
}
