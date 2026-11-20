using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Services;
using Scanner.WPF.Services;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Net.Http.Headers;

namespace Scanner.Web.Controllers;

[AllowAnonymous]
public sealed class BluettiProductsController : Controller
{
    [HttpGet("/bluetti-products")]
    public IActionResult Index(string? q, int page = 1) =>
        SearchPage(nameof(Index), q, page, BluettiProductSearch.Search);

    [HttpGet("/shipping-skus")]
    public IActionResult ShippingSkus() => View(nameof(ShippingSkus), false);

    [HttpGet("/manual-accessories")]
    public IActionResult ManualAccessories() => View(nameof(ShippingSkus), true);

    private static readonly Lazy<byte[]> ShippingSnapshot = new(() =>
        JsonSerializer.SerializeToUtf8Bytes(ShippingSkuCatalog.Entries.Select(row => new
        {
            row.Sku, row.Description, row.Category, row.Model, row.Length, row.Width, row.Height,
            row.Weight, row.LengthInches, row.WidthInches, row.HeightInches, row.WeightPounds,
            SearchText = ShippingSkuCatalog.Normalize(string.Join(" ", row.Sku, row.Description,
                row.Category, row.Model, row.MaterialNumber, row.MaterialName, row.Version,
                row.Upc, row.Ean, row.Jan, row.Specification))
        }), new JsonSerializerOptions(JsonSerializerDefaults.Web)));

    private static readonly Lazy<EntityTagHeaderValue> ShippingVersion = new(() =>
        new EntityTagHeaderValue("\"" + Convert.ToHexString(SHA256.HashData(ShippingSnapshot.Value)) + "\""));

    [HttpGet("/api/catalogs/shipping-skus")]
    public IActionResult ShippingSkuSnapshot()
    {
        Response.Headers.CacheControl = "private, no-cache";
        return new FileContentResult(ShippingSnapshot.Value, "application/json; charset=utf-8")
        {
            EntityTag = ShippingVersion.Value
        };
    }

    private IActionResult SearchPage(string view, string? q, int page, Func<string, ShippingSkuEntry[]> search)
    {
        const int pageSize = 50;
        string query = q?.Trim() ?? "";
        if (query.Length > 120)
            return BadRequest("搜索词最多 120 个字符。");

        ShippingSkuEntry[] matches = query.Length == 0 ? [] : search(query);
        int pageCount = Math.Max(1, (matches.Length + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, pageCount);
        return View(view, new BluettiProductsPage(query, page, pageCount, matches.Length,
            matches.Skip((page - 1) * pageSize).Take(pageSize).ToArray()));
    }
}

public sealed record BluettiProductsPage(
    string Query, int Page, int PageCount, int MatchCount, ShippingSkuEntry[] Products);
