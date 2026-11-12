using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using Scanner.Server.DAL;
using Scanner.Server.Model;
using Scanner.Web.Controllers;

var api = new RepairScansController(new UnusedDatabase());
var valid = new RepairScan(Guid.NewGuid(), "000123", "SKU", DateTimeOffset.UtcNow);
foreach (var invalid in new[] {
    valid with { Sn = "" }, valid with { Sku = "  " },
    valid with { Sn = new string('S', 51) }, valid with { Sku = new string('K', 51) },
    valid with { Sn = "S\0N" }, valid with { ScanId = Guid.Empty },
    valid with { StartedAt = default }, valid with { StartedAt = DateTimeOffset.UtcNow.AddHours(1) }
})
    if (await api.Upload(invalid, default) is not BadRequestObjectResult)
        throw new Exception("Invalid repair scan accepted");
foreach (var accepted in new[] { valid, valid with { Sn = new string('S', 50), Sku = new string('K', 50) } })
{
    try { await api.Upload(accepted, default); throw new Exception("Valid input rejected"); }
    catch (DatabaseReached) { }
}
Console.WriteLine("PASS: required pair, 50/51-character boundaries, scan ID, control characters and timestamp validation.");

sealed class DatabaseReached : Exception { }
sealed class UnusedDatabase : IMySqlConnectionFactory
{
    public Task<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default) => throw new DatabaseReached();
}
