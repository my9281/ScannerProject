using MySqlConnector;
using Scanner.Server.Model;

namespace Scanner.Server.DAL;

public sealed class OrderRepository(IMySqlConnectionFactory connectionFactory) : IOrderRepository
{
    public async Task<Order?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT `id`, `dtype`, `sn`, `doid`, `dtime`, `dnote` FROM `orders` WHERE `id` = @id LIMIT 1;";
        command.Parameters.AddWithValue("@id", id);
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? Read(reader) : null;
    }

    public async Task<Order> CreateAsync(SaveOrderRequest request, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "INSERT INTO `orders` (`dtype`, `sn`, `doid`, `dtime`, `dnote`) VALUES (@dtype, @sn, @doid, @dtime, @dnote);";
        AddValues(command, request);
        await command.ExecuteNonQueryAsync(cancellationToken);
        return new Order(checked((int)command.LastInsertedId), request.Dtype, request.Sn, request.Doid, request.Dtime, request.Dnote);
    }

    public async Task<bool> UpdateAsync(int id, SaveOrderRequest request, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "UPDATE `orders` SET `dtype` = @dtype, `sn` = @sn, `doid` = @doid, `dtime` = @dtime, `dnote` = @dnote WHERE `id` = @id;";
        command.Parameters.AddWithValue("@id", id);
        AddValues(command, request);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM `orders` WHERE `id` = @id;";
        command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
    }

    private static void AddValues(MySqlCommand command, SaveOrderRequest request)
    {
        command.Parameters.Add("@dtype", MySqlDbType.VarChar, 50).Value = request.Dtype;
        command.Parameters.Add("@sn", MySqlDbType.VarChar, 50).Value = request.Sn;
        command.Parameters.Add("@doid", MySqlDbType.VarChar, 50).Value = request.Doid;
        command.Parameters.Add("@dtime", MySqlDbType.DateTime).Value = request.Dtime;
        command.Parameters.Add("@dnote", MySqlDbType.VarChar, 50).Value = request.Dnote;
    }

    private static Order Read(MySqlDataReader reader) => new(
        reader.GetInt32("id"),
        reader.GetString("dtype"),
        reader.GetString("sn"),
        reader.GetString("doid"),
        reader.GetDateTime("dtime"),
        reader.GetString("dnote"));
}
