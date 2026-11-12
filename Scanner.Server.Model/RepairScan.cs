namespace Scanner.Server.Model;

public sealed record RepairScan(Guid ScanId, string Sn, string Sku, DateTimeOffset StartedAt);
