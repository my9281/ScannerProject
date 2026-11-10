using MySqlConnector;

namespace Scanner.Server.DAL;

public interface IMySqlConnectionFactory
{
    Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}
