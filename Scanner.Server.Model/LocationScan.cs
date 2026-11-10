namespace Scanner.Server.Model;
public sealed record LocationScanItem(string LocationId, DateTimeOffset ScannedAt);
public sealed record LocationBatchRequest(IReadOnlyList<LocationScanItem> Items);
public sealed record LocationBatchResult(IReadOnlyList<string> AcceptedLocationIds);
public sealed record LocationRow(string LocationId, bool IsDisabled, DateTime ScannedAt);
public sealed record LocationSkuRow(string Sku, long Quantity);
public sealed record LocationSkuChange(string LocationId, string Sku, int Delta, Guid OperationId, string Operator);
