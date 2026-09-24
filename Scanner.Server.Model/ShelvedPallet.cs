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
    TimeOnly ShelvingTime);

public sealed record ShelvedPalletItemRequest(
    int Number,
    string Sn,
    string Sku,
    string? Type,
    string? ProcessingMethod,
    string? ProcessingTime);

public sealed record CreateShelvedPalletBatchRequest(
    string PalletNumber,
    IReadOnlyList<ShelvedPalletItemRequest> Items);

public sealed record CreateShelvedPalletBatchResult(
    string PalletNumber,
    DateTime ShelvedAt,
    int InsertedCount,
    IReadOnlyList<string> Uuids);

public sealed record ShelvedPalletTableResult(
    IReadOnlyList<string> Columns,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int Count);
