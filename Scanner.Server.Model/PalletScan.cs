namespace Scanner.Server.Model;

public sealed record PalletScanItem(Guid ScanId, string Sn, int PalletNumber, DateTimeOffset ScannedAt);
public sealed record PalletScanBatchRequest(IReadOnlyList<PalletScanItem> Items);
public sealed record PalletScanBatchResult(IReadOnlyList<Guid> AcceptedScanIds);
