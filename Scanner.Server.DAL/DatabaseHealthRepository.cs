namespace Scanner.Server.DAL;

public sealed class DatabaseHealthRepository(IMySqlConnectionFactory connectionFactory) : IDatabaseHealthRepository
{
    public async Task<string> GetServerVersionAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT VERSION();";
        object? result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToString(result) ?? "unknown";
    }
}
