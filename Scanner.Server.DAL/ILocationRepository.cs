using Scanner.Server.Model;
namespace Scanner.Server.DAL;
public interface ILocationRepository
{
    Task SaveAsync(IReadOnlyList<LocationScanItem> items, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LocationRow>> GetAllAsync(CancellationToken cancellationToken = default);
}

