using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scanner.Web.Services;
using System.Xml.Linq;

namespace Scanner.Web.Controllers;

[AllowAnonymous]
public sealed class PublicSiteController : Controller
{
    [HttpGet("/")]
    public IActionResult Index() => View();

    [HttpGet("/index.html")]
    public IActionResult LegacyIndex() => RedirectPermanent($"{Request.PathBase}/{Request.QueryString}");

    [HttpGet("/sitemap.xml")]
    public IActionResult Sitemap()
    {
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var xml = new XElement(ns + "urlset", PublicSiteSeo.Pages.Select(page =>
            new XElement(ns + "url", new XElement(ns + "loc", PublicSiteSeo.AbsoluteUrl(Request, page.Path)))));
        return Content(xml.ToString(), "application/xml; charset=utf-8");
    }

    [HttpGet("/robots.txt")]
    public IActionResult Robots()
    {
        var paths = PublicSiteSeo.Pages.Select(page => $"Allow: {Request.PathBase}{page.Path}$");
        var publicAssets = "Allow: /*.css\nAllow: /*.js\nAllow: /shipping-cow-icon.png$\nAllow: /shipping-cow.png$\nAllow: /manual-accessories-data.json$\nAllow: /api/catalogs/shipping-skus$";
        return Content("User-agent: *\nDisallow: /\n" + string.Join('\n', paths)
            + "\n" + publicAssets + "\nSitemap: " + PublicSiteSeo.AbsoluteUrl(Request, "/sitemap.xml") + "\n",
            "text/plain; charset=utf-8");
    }
}
