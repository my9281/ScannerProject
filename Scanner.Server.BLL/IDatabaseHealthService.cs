using Scanner.Server.Model;

namespace Scanner.Server.BLL;

public interface IDatabaseHealthService
{
    Task<DatabaseHealthStatus> CheckAsync(CancellationToken cancellationToken = default);
}
