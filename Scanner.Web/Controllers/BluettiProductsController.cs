using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Services;
using Scanner.WPF.Services;

namespace Scanner.Web.Controllers;

[AllowAnonymous]
public sealed class BluettiProductsController : Controller
{
    [HttpGet("/bluetti-products")]
    public IActionResult Index(string? q, int page = 1)
    {
        const int pageSize = 50;
        string query = q?.Trim() ?? "";
        if (query.Length > 120)
            return BadRequest("搜索词最多 120 个字符。");

        ShippingSkuEntry[] matches = query.Length == 0 ? [] : BluettiProductSearch.Search(query);
        int pageCount = Math.Max(1, (matches.Length + pageSize - 1) / pageSize);
        page = Math.Clamp(page, 1, pageCount);
        return View(new BluettiProductsPage(query, page, pageCount, matches.Length,
            matches.Skip((page - 1) * pageSize).Take(pageSize).ToArray()));
    }
}

public sealed record BluettiProductsPage(
    string Query, int Page, int PageCount, int MatchCount, ShippingSkuEntry[] Products);
