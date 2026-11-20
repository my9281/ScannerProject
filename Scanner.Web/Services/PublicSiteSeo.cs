namespace Scanner.Web.Services;

public sealed record PublicSiteSeo(string Path, string Title, string Description)
{
    public static readonly PublicSiteSeo Home = new("/", "维修管理平台",
        "维修管理平台提供 BLUETTI 产品、用户手册配件和发货 SKU 查询，以及维修收货、包裹对账和托盘查询工具。");
    public static readonly PublicSiteSeo Products = new("/bluetti-products", "BLUETTI 产品查询",
        "按 SKU、型号、料号或条码查找 BLUETTI 产品资料和产品规格。");
    public static readonly PublicSiteSeo Shipping = new("/shipping-skus", "配件与发货 SKU 查询",
        "按 SKU、型号或描述查询配件和整机的包装尺寸、重量及发货资料。");
    public static readonly PublicSiteSeo Accessories = new("/manual-accessories", "BLUETTI 手册配件查询",
        "按型号、配件名称或规格搜索 BLUETTI 手册中的随箱配件和选购件，并回查原 PDF 页码。");

    public static IReadOnlyList<PublicSiteSeo> Pages { get; } = [Home, Products, Shipping, Accessories];
    public string PageTitle => Path == "/" ? "维修管理平台 | ShippingCow" : Title + " | 维修管理平台";

    public static string AbsoluteUrl(HttpRequest request, string path)
    {
        var configured = request.HttpContext.RequestServices.GetRequiredService<IConfiguration>()["Seo:Origin"];
        string origin = Uri.TryCreate(configured, UriKind.Absolute, out var uri)
            && uri.Scheme is "https" or "http"
            && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0 && uri.UserInfo.Length == 0
            ? uri.GetLeftPart(UriPartial.Authority)
            : $"{request.Scheme}://{request.Host.ToUriComponent()}";
        return origin + request.PathBase.ToUriComponent() + path;
    }
}
