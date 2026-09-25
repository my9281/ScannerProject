using Microsoft.AspNetCore.Mvc;
using Scanner.Server.BLL;
using Scanner.Server.Model;
using Scanner.Web.Filters;
using Scanner.Web.Services;
using Microsoft.Extensions.Options;

namespace Scanner.Web.Controllers;

[ApiController]
[Route("api/shelved-pallets")]
[ApiKey]
public sealed class ShelvedPalletsController(
    IShelvedPalletService pallets,
    IFeishuRobotService feishu,
    IOptions<FeishuRobotOptions> feishuOptions) : ControllerBase
{
    [HttpGet("all")]
    public async Task<ActionResult<ShelvedPalletTableResult>> GetAll(CancellationToken cancellationToken)
        => Ok(await pallets.GetAllAsync(cancellationToken));

    [HttpGet("view")]
    public async Task<ActionResult<PalletDisplayResult>> View(
        [FromQuery] DateOnly t,
        [FromQuery] string p,
        CancellationToken cancellationToken)
    {
        if (t == default || string.IsNullOrWhiteSpace(p)) return NotFound();
        string palletNumber = p.Trim();
        ShelvedPalletTableResult table = await pallets.GetAllAsync(cancellationToken);
        PalletDisplayItem[] items = table.Rows
            .Where(row => DateMatches(row, t) && string.Equals(TextValue(row, "pallet_number", "palletNumber"), palletNumber, StringComparison.Ordinal))
            .Select(row => new PalletDisplayItem(TextValue(row, "sku", "SKU"), TextValue(row, "sn", "SN")))
            .ToArray();
        return items.Length == 0 ? NotFound() : Ok(new PalletDisplayResult(t, palletNumber, items));
    }

    [HttpGet("directory")]
    public async Task<ActionResult<PalletDirectoryResult>> Directory(
        [FromQuery] DateOnly ts,
        [FromQuery] DateOnly te,
        CancellationToken cancellationToken)
    {
        if (ts == default || te == default || ts > te) return NotFound();
        ShelvedPalletTableResult table = await pallets.GetAllAsync(cancellationToken);
        PalletDirectoryItem[] result = table.Rows
            .Select(row => new
            {
                Date = ReadDate(row),
                Pallet = TextValue(row, "pallet_number", "palletNumber"),
                Sku = TextValue(row, "sku", "SKU")
            })
            .Where(item => item.Date.HasValue && item.Date.Value >= ts && item.Date.Value <= te && !string.IsNullOrWhiteSpace(item.Pallet))
            .GroupBy(item => new { Date = item.Date!.Value, item.Pallet })
            .Select(group => new PalletDirectoryItem(
                group.Key.Date,
                group.Key.Pallet,
                group.Count(),
                group.Select(item => item.Sku).Where(sku => !string.IsNullOrWhiteSpace(sku)).Distinct(StringComparer.OrdinalIgnoreCase).Count()))
            .OrderByDescending(item => item.ShelvingDate)
            .ThenBy(item => item.PalletNumber, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return Ok(new PalletDirectoryResult(ts, te, result));
    }

    [HttpPost]
    public async Task<ActionResult<CreateShelvedPalletBatchResult>> Create(
        [FromBody] CreateShelvedPalletBatchRequest request,
        CancellationToken cancellationToken)
    {
        CreateShelvedPalletBatchResult result = await pallets.CreateBatchAsync(request, cancellationToken);
        string baseUrl = string.IsNullOrWhiteSpace(feishuOptions.Value.PublicBaseUrl)
            ? $"{Request.Scheme}://{Request.Host}{Request.PathBase}"
            : feishuOptions.Value.PublicBaseUrl.TrimEnd('/');
        string pageUrl = $"{baseUrl}/pallet-data.html?t={result.ShelvedAt:yyyy-MM-dd}&p={Uri.EscapeDataString(result.PalletNumber)}";
        FeishuSkuItem[] skuItems = request.Items
            .Where(item => !string.IsNullOrWhiteSpace(item.Sku))
            .GroupBy(item => item.Sku.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(group => new FeishuSkuItem(group.Key, group.Count()))
            .OrderBy(item => item.Sku, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        await feishu.SendShelvedPalletAsync(result.PalletNumber, result.ShelvedAt, result.InsertedCount, skuItems, pageUrl, CancellationToken.None);
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ShelvedPalletRecord>>> Query(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] string? palletNumber,
        [FromQuery] DateOnly? date,
        [FromQuery(Name = "t")] DateOnly? shortDate,
        [FromQuery(Name = "p")] string? shortPalletNumber,
        [FromQuery] int limit = 500,
        CancellationToken cancellationToken = default)
    {
        date ??= shortDate;
        if (date.HasValue)
        {
            from ??= date.Value.ToDateTime(TimeOnly.MinValue);
            to ??= date.Value.ToDateTime(TimeOnly.MaxValue);
        }
        palletNumber = string.IsNullOrWhiteSpace(palletNumber) ? shortPalletNumber : palletNumber;
        return Ok(await pallets.QueryAsync(from, to, palletNumber, limit, cancellationToken));
    }

    private static bool DateMatches(IReadOnlyDictionary<string, object?> row, DateOnly expected)
        => ReadDate(row) == expected;

    private static DateOnly? ReadDate(IReadOnlyDictionary<string, object?> row)
    {
        foreach (string name in new[] { "shelving_date", "shelvingDate" })
        {
            if (!row.TryGetValue(name, out object? value) || value is null) continue;
            if (value is DateTime dateTime) return DateOnly.FromDateTime(dateTime);
            if (value is DateOnly dateOnly) return dateOnly;
            if (DateTime.TryParse(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture),
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dateTime))
                return DateOnly.FromDateTime(dateTime);
        }
        return null;
    }

    private static string TextValue(IReadOnlyDictionary<string, object?> row, params string[] names)
    {
        foreach (string name in names)
            if (row.TryGetValue(name, out object? value)) return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        return string.Empty;
    }
}
