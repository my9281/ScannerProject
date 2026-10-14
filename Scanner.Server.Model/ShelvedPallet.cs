namespace Scanner.Server.Model;

public sealed record ShelvedPalletRecord(
    string Uuid,
    int Number,
    string Sn,
    string Sku,
    string? Type,
    string? ProcessingMethod,
    DateTime? ProcessingTime,
    DateOnly ShelvingDate,
    string PalletNumber,
    TimeOnly ShelvingTime, bool IsReplay = false);

public sealed record ShelvedPalletItemRequest(
    int Number,
    string Sn,
    string Sku,
    string? Type,
    string? ProcessingMethod,
    string? ProcessingTime);

public sealed record CreateShelvedPalletBatchRequest(
    string PalletNumber,
    IReadOnlyList<ShelvedPalletItemRequest> Items, Guid? RequestId = null);

public sealed record CreateShelvedPalletBatchResult(
    string PalletNumber,
    DateTime ShelvedAt,
    int InsertedCount,
    IReadOnlyList<string> Uuids, Guid? RequestId = null, bool IsReplay = false);

public sealed record ShelvedPalletTableResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int Count);

public sealed record PalletDisplayItem(string Sku, string Sn);

public sealed record PalletDisplayResult(
    DateOnly ShelvingDate,
    string PalletNumber,
    IReadOnlyList<PalletDisplayItem> Items);

public sealed record PalletDirectoryItem(
    DateOnly ShelvingDate,
    string PalletNumber,
    int ItemCount,
    int SkuCount);

public sealed record PalletDirectoryResult(
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<PalletDirectoryItem> Pallets);
