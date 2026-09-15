using MySqlConnector;
using Scanner.Server.Model;

namespace Scanner.Server.DAL;

public sealed class MySqlConnectionFactory(DatabaseOptions options) : IMySqlConnectionFactory
{
    private readonly string _connectionString = BuildConnectionString(options);

    public async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (!options.Enabled)
        {
            throw new InvalidOperationException("MySQL access is disabled. Set Database:Enabled to true to enable it.");
        }

        MySqlConnection connection = new(_connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private static string BuildConnectionString(DatabaseOptions options)
    {
        if (!Enum.TryParse(options.SslMode, true, out MySqlSslMode sslMode))
        {
            throw new InvalidOperationException($"Unsupported Database:SslMode value: {options.SslMode}");
        }

        return new MySqlConnectionStringBuilder
        {
            Server = options.Server,
            Port = options.Port,
            Database = options.Database,
            UserID = options.UserId,
            Password = options.Password,
            SslMode = sslMode,
            ConnectionTimeout = options.ConnectionTimeout,
            DefaultCommandTimeout = options.CommandTimeout,
            AllowUserVariables = true
        }.ConnectionString;
    }
}
