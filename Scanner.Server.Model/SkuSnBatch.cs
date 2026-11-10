namespace Scanner.Server.Model;
public sealed record SkuSnItem(string Sku, string Sn);
public sealed record SkuSnBatch(Guid BatchId, string BatchNumber, DateTimeOffset CreatedAt, IReadOnlyList<SkuSnItem> Items);
public sealed record SkuSnBatchSummary(Guid BatchId, string BatchNumber, DateTimeOffset CreatedAt, int ItemCount);
