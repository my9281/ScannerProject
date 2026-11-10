using Scanner.Server.DAL;
using Scanner.Server.Model;

namespace Scanner.Server.BLL;

public sealed class DatabaseHealthService(IDatabaseHealthRepository repository, DatabaseOptions options) : IDatabaseHealthService
{
    public async Task<DatabaseHealthStatus> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            return new(false, "disabled", Error: "MySQL access is disabled in configuration.");
        }

        try
        {
            string serverVersion = await repository.GetServerVersionAsync(cancellationToken);
            return new(true, "ok", serverVersion);
        }
        catch (Exception exception)
        {
            return new(false, "unavailable", Error: exception.Message);
        }
    }
}
