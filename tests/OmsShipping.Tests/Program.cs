using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Newtonsoft.Json.Linq;
using Scanner.WPF.Services;

internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        int exit = 1;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += async (_, _) => { try { await Test(args); exit = 0; } catch (Exception ex) { Console.Error.WriteLine(ex); } finally { app.Shutdown(); } };
        app.Run(); return exit;
    }
    static async Task Test(string[] args)
    {
        Check(ShippingSkuCatalog.Entries.Count == 11983, "SKU source row count");
        var sku = ShippingSkuCatalog.Entries[0];
        Check(ShippingSkuCatalog.Search(sku.Sku.ToLowerInvariant()).Any(r => r.Sku == sku.Sku), "Case insensitive SKU lookup");
        Check(ShippingSkuCatalog.Search("PV61.5KW 配件包").Any(r => r.Sku == sku.Sku), "Description multi keyword lookup");
        Check(sku.Length == "" && sku.Width == "" && sku.Height == "", "Missing dimensions remain blank");
        Check(!sku.HasShippingMeasurements, "Empty package measurements cannot be applied");
        var measuredSku = ShippingSkuCatalog.Search("P-EL200V2-CL-GY-10").First();
        Check(measuredSku.LengthInches == 17 && measuredSku.WidthInches == 13 &&
            measuredSku.HeightInches == 19 && measuredSku.WeightPounds == 52, "Package measurements round up after conversion");
        Check(ShippingSkuCatalog.Search("不存在的SKU-xyz123").Length == 0, "No match");
        Check(ShippingSkuCatalog.Search("  ").Length == ShippingSkuCatalog.Entries.Count, "Empty query");
        string root = Path.Combine(Environment.CurrentDirectory, "output", "OmsCodeValidation"); Directory.CreateDirectory(root);
        var shipper = new JObject { ["shipperName"] = "Sender", ["shipperAddress1"] = "520 Jersey Avenue", ["shipperPostCode"] = "08901", ["shipperCountryCode"] = "US" };
        var shipment = new OmsShipment { CustomerNo = "test-order-" + Guid.NewGuid().ToString("N"), Name = "Test", Email = "test@example.com", Phone = "001234", Address = "9440 Westfall   Road/Amanda/OH/43102-9766/美国", Length = 16, Width = 12, Height = 16, Weight = 30 };
        var payload = shipment.Build(shipper, "product-test", new JValue("3"));
        Check((string)payload["consigneeShipper"]["consigneePostCode"] == "43102", "ZIP suffix");
        Check((string)payload["consigneeShipper"]["consigneeAddress1"] == "9440 Westfall Road", "Street whitespace");
        Check((string)payload["currencyCode"] == "USD" && (string)payload["weightSizeUnit"] == "3", "USD/INCH-LB mapping");
        Check((int)payload["orderDetailList"][0]["boxQuantity"] == 1 && (decimal)payload["orderDetailList"][0]["boxWeight"] == 30, "Package mapping");
        Check((string)payload["consigneeShipper"]["shipperPostCode"] == "08901" && (string)payload["consigneeShipper"]["consigneePhone"] == "001234", "Leading zero identifiers");
        shipment.Weight = -1; try { shipment.Build(shipper, "test", 3); throw new Exception("Negative weight accepted"); } catch (ArgumentException) { } shipment.Weight = 30;
        var handler = new MockHandler(); using var api = new OmsShippingService(new HttpClient(handler));
        await api.LoginAsync("test", "secret-not-logged", CancellationToken.None);
        Check((await api.ProductsAsync(CancellationToken.None)).Count == 1, "Products");
        Check((await api.ShippersAsync(CancellationToken.None)).Count == 1, "Shippers");
        Check((string)await api.InchPoundUnitAsync(CancellationToken.None) == "3", "Units from dict");
        var quote = await api.QuoteAsync(payload, CancellationToken.None);
        Check((decimal)quote["orderFeeDetail"]["settleTotalPrice"] == 17.63m, "Quote mapping");
        var paid = await api.PayOnceAsync(payload, Path.Combine(root, "test-ledger"), CancellationToken.None);
        Check((string)paid["id"] == "test-id", "Payment result");
        try { await api.PayOnceAsync(payload, Path.Combine(root, "test-ledger"), CancellationToken.None); throw new Exception("Duplicate pay accepted"); } catch (IOException) { }
        Check(handler.PayRequests == 1, "Payment was repeated");
        var uri = await api.GetLabelAsync("test-id", CancellationToken.None);
        Check(uri.Host == "erp-us-oss.eccang.com", "Label URL");
        await api.DownloadLabelAsync(uri, Path.Combine(root, "mock-download.pdf"), CancellationToken.None);
        Check(!handler.StorageHadToken, "OMS token leaked to PDF host");
        try { OmsShippingService.ValidateLabelUri(new Uri("https://evil.invalid/label.pdf")); throw new Exception("Unexpected host accepted"); } catch (InvalidDataException) { }
        Check(handler.Calls.Any(c => c == "POST /alpaca-oms/oms/order/submit") && handler.Calls.Any(c => c == "POST /alpaca-oms/oms/order/payOrder") && handler.Calls.Any(c => c == "GET /alpaca-oms/oms/order/getLabel"), "HTTP request methods");

        // Simulate an ambiguous payment timeout; the persisted marker must block another attempt.
        handler.FailPayment = true; payload["customerNo"] = "timeout-" + Guid.NewGuid().ToString("N");
        try { await api.PayOnceAsync(payload, Path.Combine(root, "test-ledger"), CancellationToken.None); throw new Exception("Timeout expected"); } catch (HttpRequestException) { }
        int attempted = handler.PayRequests;
        try { await api.PayOnceAsync(payload, Path.Combine(root, "test-ledger"), CancellationToken.None); throw new Exception("Ambiguous payment retried"); } catch (IOException) { }
        Check(handler.PayRequests == attempted, "Uncertain payment repeated");

        if (args.Length == 1)
        {
            var original = await OmsLabelDocument.RenderAsync(args[0], "", "", "");
            var annotated = await OmsLabelDocument.RenderAsync(args[0], "LOG-CRP26101417", "EL100V2", "2523011398645");
            OmsLabelDocument.SavePng(annotated, Path.Combine(root, "label.png"));
            OmsLabelDocument.SavePdf(annotated, Path.Combine(root, "label.pdf"));
            Check(annotated.PixelWidth == 1200 && annotated.PixelHeight == 1800, "300 DPI label size");
            int stride = 1200 * 4; var a = new byte[stride * 1800]; var b = new byte[a.Length]; original.CopyPixels(a, stride, 0); annotated.CopyPixels(b, stride, 0);
            int changed = 0;
            for (int y = 0; y < 1800; y++) for (int x = 0; x < 1200; x++)
            {
                int p = y * stride + x * 4;
                if (a[p] == b[p] && a[p + 1] == b[p + 1] && a[p + 2] == b[p + 2]) continue;
                changed++;
                Check(x >= 700 && x <= 1075 && y >= 245 && y <= 430, "Pixels changed outside recipient annotation region");
            }
            Check(changed > 100, "Annotation missing");
            var reopened = await OmsLabelDocument.RenderAsync(Path.Combine(root, "label.pdf"), "", "", "");
            Check(reopened.PixelWidth == 1200 && reopened.PixelHeight == 1800, "Exported PDF cannot reopen");
            Console.WriteLine("PASS: native PDF rendering, 300 DPI PNG/PDF export, recipient-side annotation, every pixel outside annotation region unchanged; exported PDF reopened.");
        }
        Console.WriteLine("PASS: payload mapping, API contracts, first-address/unit retrieval, fee result, no token forwarding, one payment per order, uncertain-payment protection. No real order or print job submitted.");
    }
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
sealed class MockHandler : HttpMessageHandler
{
    public int PayRequests; public bool StorageHadToken, FailPayment;
    public List<string> Calls = new();
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri!.AbsolutePath; Calls.Add(request.Method.Method + " " + path);
        if (request.RequestUri.Host == "erp-us-oss.eccang.com") { StorageHadToken = request.Headers.Contains("X-Access-Token"); return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("%PDF-test") }; }
        JToken result;
        if (path.EndsWith("/login"))
        {
            var data = JObject.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            if ((int)data["systemBelong"] != 3) throw new Exception("Login payload");
            return Json(new JObject { ["code"] = 1, ["result"] = new JObject { ["token"] = "fake-token" } });
        }
        if (request.Headers.GetValues("X-Access-Token").Single() != "fake-token") throw new Exception("Missing auth");
        if (path.EndsWith("/getCommonProduct")) result = new JArray(new JObject { ["id"] = "product-test", ["name"] = "FedEx-美东-NJ" });
        else if (path.EndsWith("/all")) result = new JArray(new JObject { ["shipperAddress1"] = "520 Jersey Avenue" });
        else if (path.EndsWith("/weight_size_unit")) result = new JArray(new JObject { ["value"] = "1", ["text"] = "CM/KG" }, new JObject { ["value"] = "3", ["text"] = "英制(INCH/LB)" });
        else if (path.EndsWith("/submit")) result = new JObject { ["orderFeeDetail"] = new JObject { ["settleTotalPrice"] = 17.63, ["settleCurrency"] = "USD" } };
        else if (path.EndsWith("/payOrder")) { PayRequests++; if (FailPayment) throw new HttpRequestException("Simulated timeout"); result = new JObject { ["id"] = "test-id", ["realPrice"] = 17.63 }; }
        else if (path.EndsWith("/getLabel")) result = new JObject { ["mergeUrl"] = "https://erp-us-oss.eccang.com/test.pdf" };
        else throw new Exception("Unexpected API");
        return Json(new JObject { ["success"] = true, ["result"] = result });
    }
    static HttpResponseMessage Json(JObject value) => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(value.ToString(), Encoding.UTF8, "application/json") };
}
