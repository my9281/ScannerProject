namespace Scanner.Web.Services;

public interface IFeishuRobotService
{
    Task<bool> SendShelvedPalletAsync(string palletNumber, DateTime shelvedAt, int count, IReadOnlyList<FeishuSkuItem> skuItems, string pageUrl, CancellationToken cancellationToken = default);
    Task<bool> SendTestAsync(string message, string? url, CancellationToken cancellationToken = default);
}

public sealed record FeishuSkuItem(string Sku, int Quantity);
