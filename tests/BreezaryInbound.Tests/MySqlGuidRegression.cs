using System.Net;
using System.Net.Sockets;
using System.Text;
using MySqlConnector;
using Scanner.Server.DAL;
using Scanner.Server.Model;
using Scanner.Web.Services;

// A minimal text-protocol fixture exercises the real MySqlConnector reader and
// production store, including CHAR(36) -> Guid conversion. No production DB used.
static class MySqlGuidRegression
{
    public static async Task RunAsync(Action<bool, string> check)
    {
        var item = new BreezaryInboundItem(Guid.NewGuid(), Guid.NewGuid(), 12, "GUID-TEST", new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        int commits = 0, rollbacks = 0;
        var server = Task.Run(async () =>
        {
            for (int session = 0; session < 3; session++)
            {
                using var socket = await listener.AcceptTcpClientAsync(timeout.Token);
                await using var stream = socket.GetStream();
                byte sequence = 0;
                async Task Packet(byte[] data)
                {
                    await stream.WriteAsync(new byte[] { (byte)data.Length, (byte)(data.Length >> 8), (byte)(data.Length >> 16), sequence++ }, timeout.Token);
                    await stream.WriteAsync(data, timeout.Token);
                }
                async Task<byte[]> Read()
                {
                    byte[] header = new byte[4];
                    await stream.ReadExactlyAsync(header, timeout.Token);
                    byte[] data = new byte[header[0] | header[1] << 8 | header[2] << 16];
                    await stream.ReadExactlyAsync(data, timeout.Token);
                    sequence = (byte)(header[3] + 1);
                    return data;
                }
                using var handshake = new MemoryStream();
                using (var writer = new BinaryWriter(handshake, Encoding.UTF8, true))
                {
                    writer.Write((byte)10); writer.Write(Encoding.ASCII.GetBytes("8.0.0-fixture\0"));
                    writer.Write(1); writer.Write(Encoding.ASCII.GetBytes("12345678")); writer.Write((byte)0);
                    writer.Write((ushort)0x8201); writer.Write((byte)45); writer.Write((ushort)2);
                    writer.Write((ushort)8); writer.Write((byte)21); writer.Write(new byte[10]);
                    writer.Write(Encoding.ASCII.GetBytes("123456789012\0mysql_native_password\0"));
                }
                await Packet(handshake.ToArray());
                await Read();
                byte[] ok = [0, 0, 0, 2, 0, 0, 0], eof = [254, 0, 0, 2, 0];
                await Packet(ok);
                while (true)
                {
                    var command = await Read();
                    if (command[0] == 1) break;
                    var sql = Encoding.UTF8.GetString(command, 1, command.Length - 1);
                    if (sql.StartsWith("SELECT device_id", StringComparison.Ordinal) || sql.StartsWith("SELECT id,scan_id", StringComparison.Ordinal))
                    {
                        bool list = sql.StartsWith("SELECT id,", StringComparison.Ordinal);
                        var columns = new List<(string Name, byte Type, ushort Charset, uint Length, ushort Flags, string Value)>();
                        if (list)
                        {
                            columns.Add(("id", 8, 63, 20, 32, "1"));
                            columns.Add(("scan_id", 254, 11, 36, 0, item.ScanId.ToString("D")));
                        }
                        columns.Add(("device_id", 254, 11, 36, 0, item.DeviceId.ToString("D")));
                        columns.Add(("cache_location", 1, 63, 3, 32, "12"));
                        columns.Add(("barcode", 253, 45, 100, 0, item.Barcode));
                        columns.Add(("scanned_at", 12, 63, 23, 0, "2026-10-17 00:00:00.000"));
                        if (list) columns.Add(("uploaded_at", 12, 63, 23, 0, "2026-10-17 00:00:01.000"));
                        await Packet([(byte)columns.Count]);
                        foreach (var col in columns)
                        {
                            using var field = new MemoryStream();
                            using var writer = new BinaryWriter(field, Encoding.UTF8, true);
                            foreach (var text in new[] { "def", "test", "breezarystar_inbound_scans", "breezarystar_inbound_scans", col.Name, col.Name })
                                WriteText(writer, text);
                            writer.Write((byte)12); writer.Write(col.Charset); writer.Write(col.Length);
                            writer.Write(col.Type); writer.Write(col.Flags); writer.Write((byte)0); writer.Write((ushort)0);
                            await Packet(field.ToArray());
                        }
                        await Packet(eof);
                        using var row = new MemoryStream();
                        using (var writer = new BinaryWriter(row, Encoding.UTF8, true))
                            foreach (var col in columns) WriteText(writer, col.Value);
                        await Packet(row.ToArray());
                        await Packet(eof);
                    }
                    else
                    {
                        if (sql.TrimEnd(';').Equals("COMMIT", StringComparison.OrdinalIgnoreCase)) commits++;
                        if (sql.TrimEnd(';').Equals("ROLLBACK", StringComparison.OrdinalIgnoreCase)) rollbacks++;
                        await Packet(ok);
                    }
                }
            }
        }, timeout.Token);
        var store = new MySqlBreezaryInboundStore(new Connections(((IPEndPoint)listener.LocalEndpoint).Port));
        await store.SaveAsync([item], timeout.Token);
        check(commits == 1, "Real MySqlConnector CHAR(36) identity verification commits upload");
        var page = await store.ListAsync(0, 100, null, null, timeout.Token);
        check(page.Records.Single().ScanId == item.ScanId && page.Records[0].DeviceId == item.DeviceId,
            "Web list reads CHAR(36) scan and device IDs as Guid");
        try
        {
            await store.SaveAsync([item with { DeviceId = Guid.NewGuid() }], timeout.Token);
            throw new Exception("Changed device accepted");
        }
        catch (ApiException ex) when (ex.StatusCode == 409) { }
        await server;
        check(commits == 1 && rollbacks == 1, "Identity mismatch still rolls back without acknowledgement");
    }

    private static void WriteText(BinaryWriter writer, string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        writer.Write((byte)bytes.Length); writer.Write(bytes);
    }

    private sealed class Connections(int port) : IMySqlConnectionFactory
    {
        public async Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new MySqlConnection($"Server=127.0.0.1;Port={port};User ID=test;SSL Mode=None;Pooling=False;Connection Timeout=5");
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
