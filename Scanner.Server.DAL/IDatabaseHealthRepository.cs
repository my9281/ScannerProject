namespace Scanner.Server.DAL;

public interface IDatabaseHealthRepository
{
    Task<string> GetServerVersionAsync(CancellationToken cancellationToken = default);
}
