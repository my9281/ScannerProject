using System.IO.Compression;
using System.IO;
using System.Xml.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using Scanner.WPF.Services;

if (args.Length >= 2 && args[0] == "--live-query")
{
    var session = new BluettiSessionService(sessionFile: Path.Combine(args[1], "session.dat")).Load() ?? throw new Exception("No saved session");
    var api = new BluettiLabelApi(new HttpClient(new ShapeHandler()));
    var result = await api.QueryAsync(session, "AC3002222000215036", CancellationToken.None);
    if (result == null) throw new Exception("Live query returned no exact match");
    if (result.Sn != "AC3002222000215036" || result.Rma != "LOG-CRP26100294" || result.WorkOrder != "afsa261005163754987mu") throw new Exception("Live row identity mismatch");
    Console.WriteLine("PASS: live API exact SN/RMA/work-order identity; required label fields present.");
    if (args.Length == 3) { BluettiLabelRow.Append(args[2], result); Console.WriteLine("PASS: live row appended to Excel."); }
    return;
}

var directory = Path.Combine(Environment.CurrentDirectory, "output", "BluettiLabelTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(directory);
try
{
    var path = Path.Combine(directory, "labels.xlsx");
    var row = new BluettiLabelRow { Date = "2026/10/15", Sn = "AC200L2452001024945", Sku = "P-AC200L-US-GY-BL-011", Model = "AC200L", Phone = "0013142829402", Name = "测试 & Name", Remark = "=1+1", Rma = "RMA-test", WorkOrder = "afsa-test" };
    BluettiLabelRow.Export(path, new[] { row });
    using (var zip = ZipFile.OpenRead(path))
    {
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        var doc = XDocument.Load(sheet);
        var records = doc.Descendants(ns + "row").ToArray();
        if (records.Length != 2) throw new Exception("Row count");
        var headers = records[0].Descendants(ns + "t").Select(t => t.Value).ToArray();
        if (!headers.SequenceEqual(BluettiLabelRow.Headers) || headers.Length != 16) throw new Exception("Column order");
        var values = records[1].Descendants(ns + "t").Select(t => t.Value).ToArray();
        if (!values.SequenceEqual(row.Values().Select(v => v ?? ""))) throw new Exception("Export round trip");
        if (doc.Descendants(ns + "f").Any()) throw new Exception("Formula injection");
        if (!records[1].Elements(ns + "c").All(c => (string)c.Attribute("t") == "inlineStr")) throw new Exception("Identifiers must be strings");
        foreach (var entry in zip.Entries) { using var stream = entry.Open(); XDocument.Load(stream); }
    }
    BluettiLabelRow.Export(path, new[] { row, row });
    using (var zip = ZipFile.OpenRead(path))
    {
        using var sheet = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
        if (XDocument.Load(sheet).Descendants().Count(e => e.Name.LocalName == "row") != 3) throw new Exception("Overwrite/batch export");
    }
    var before = File.ReadAllBytes(path);
    BluettiLabelRow.Append(path, row);
    if (ReadRows(path).Length != 4) throw new Exception("Append should keep previous rows");
    var first = ReadRows(path)[1].ToString();
    BluettiLabelRow.Append(path, row);
    if (ReadRows(path).Length != 5 || ReadRows(path)[1].ToString() != first) throw new Exception("Append changed earlier data");
    using (var zip = ZipFile.Open(path, ZipArchiveMode.Update))
    {
        using var writer = new StreamWriter(zip.CreateEntry("customXml/test.xml").Open()); writer.Write("<preserved>untouched</preserved>");
    }
    BluettiLabelRow.Append(path, row);
    using (var zip = ZipFile.OpenRead(path))
    {
        using var reader = new StreamReader(zip.GetEntry("customXml/test.xml")!.Open());
        if (reader.ReadToEnd() != "<preserved>untouched</preserved>") throw new Exception("Unrelated workbook parts lost");
    }
    var session = new BluettiSession { Token = "test-token", SiteId = "test-site" };
    const string sn = "AC3002222000215036";
    JObject Order(string id = "afsa-test", string productSn = sn) => new JObject { ["workOrderId"] = id, ["type"] = 1, ["rmaNo"] = "LOG-test", ["productMaterialVO"] = new JArray(new JObject { ["sn"] = productSn, ["sku"] = "P-AC300-US-GY-BL-010" }) };
    JObject Page(params JObject[] orders) => new JObject { ["content"] = new JArray(orders), ["totalElements"] = orders.Length };
    JObject customer = new JObject { ["customerName"] = "Test Name", ["customerEmail"] = "test@example.com", ["customerPhone"] = "001234", ["shippingAddress"] = "Shipping address", ["customerAddress"] = "Other address" };
    var handler = new FakeHandler(Page(Order()), customer);
    var api = new BluettiLabelApi(new HttpClient(handler));
    var newPath = Path.Combine(directory, "capture.xlsx");
    var capture = new BluettiLabelCaptureService(api);
    var result = await capture.CaptureAsync(session, sn, new DateTime(2026, 10, 22), newPath, CancellationToken.None);
    if (result == null || result.Sn != sn || result.Model != "AC300" || result.Address != "Shipping address" || result.Phone != "001234" || result.Date != "2026/10/22" || ReadRows(newPath).Length != 2) throw new Exception("API to Excel mapping failed");
    if (handler.Requests.Count != 2 || handler.Requests[0].Method != "POST" || handler.Requests[0].Path != BluettiLabelApi.SearchPath || handler.Requests[1].Path != BluettiLabelApi.CustomerPath || handler.Requests[1].Query != "?workOrderId=afsa-test&orderType=1" || handler.Requests[1].Body.Length != 0) throw new Exception("Website API contract mismatch");
    var payload = JObject.Parse(handler.Requests[0].Body);
    if ((string)payload["sn"] != sn || (string)payload["siteId"] != "test-site" || (int)payload["pageNo"] != 1 || (int)payload["pageSize"] != 20 || handler.Requests.Any(r => r.Auth != "test-token" || r.Resource != "test-site")) throw new Exception("SN/site/auth contract mismatch");

    // Regression: the real service returns totalElements as "1", not integer 1.
    var liveShapedPage = Page(Order()); liveShapedPage["totalElements"] = "1";
    result = await new BluettiLabelCaptureService(new BluettiLabelApi(new HttpClient(new FakeHandler(liveShapedPage, customer)))).CaptureAsync(session, sn, new DateTime(2026, 10, 22), newPath, CancellationToken.None);
    if (result == null || ReadRows(newPath).Length != 3) throw new Exception("String totalElements did not append one row");
    var stringZero = Page(); stringZero["totalElements"] = "0";
    var stringMissPath = Path.Combine(directory, "string-zero-miss.xlsx");
    if (await new BluettiLabelCaptureService(new BluettiLabelApi(new HttpClient(new FakeHandler(stringZero)))).CaptureAsync(session, sn, DateTime.Today, stringMissPath, CancellationToken.None) != null || File.Exists(stringMissPath)) throw new Exception("String zero must remain a miss");
    foreach (JToken invalidTotal in new JToken[] { "1.5", "-1", "2147483648", "garbage", JValue.CreateNull(), new JValue(1.5) })
    {
        var badPage = Page(Order()); badPage["totalElements"] = invalidTotal;
        var snapshot = File.ReadAllBytes(newPath);
        await MustFail<InvalidDataException>(new BluettiLabelCaptureService(new BluettiLabelApi(new HttpClient(new FakeHandler(badPage)))).CaptureAsync(session, sn, DateTime.Today, newPath, CancellationToken.None));
        if (!snapshot.SequenceEqual(File.ReadAllBytes(newPath))) throw new Exception("Invalid total changed workbook");
    }

    var missHandler = new FakeHandler(Page());
    var missing = new BluettiLabelCaptureService(new BluettiLabelApi(new HttpClient(missHandler)));
    var absentPath = Path.Combine(directory, "miss.xlsx");
    if (await missing.CaptureAsync(session, sn, DateTime.Today, absentPath, CancellationToken.None) != null || File.Exists(absentPath) || missHandler.Requests.Count != 1) throw new Exception("Miss created a workbook or fetched customer");
    before = File.ReadAllBytes(newPath);
    await new BluettiLabelCaptureService(new BluettiLabelApi(new HttpClient(new FakeHandler(Page())))).CaptureAsync(session, sn, DateTime.Today, newPath, CancellationToken.None);
    if (!before.SequenceEqual(File.ReadAllBytes(newPath))) throw new Exception("Miss modified existing workbook");
    await new BluettiLabelCaptureService(new BluettiLabelApi(new HttpClient(new FakeHandler(Page(Order("fuzzy", sn + "9")))))).CaptureAsync(session, sn, DateTime.Today, newPath, CancellationToken.None);
    if (!before.SequenceEqual(File.ReadAllBytes(newPath))) throw new Exception("Fuzzy SN match was written");
    await MustFail<InvalidDataException>(new BluettiLabelCaptureService(new BluettiLabelApi(new HttpClient(new FakeHandler(Page(Order(), Order("afsa-other")))))).CaptureAsync(session, sn, DateTime.Today, newPath, CancellationToken.None));
    if (!before.SequenceEqual(File.ReadAllBytes(newPath))) throw new Exception("Ambiguous match modified workbook");
    var noShipping = (JObject)customer.DeepClone(); noShipping.Remove("shippingAddress");
    result = await new BluettiLabelApi(new HttpClient(new FakeHandler(Page(Order()), noShipping))).QueryAsync(session, sn, CancellationToken.None);
    if (result.Address != "Other address") throw new Exception("Customer address fallback failed");
    var incomplete = (JObject)customer.DeepClone(); incomplete.Remove("shippingAddress"); incomplete.Remove("customerAddress");
    await MustFail<InvalidDataException>(new BluettiLabelCaptureService(new BluettiLabelApi(new HttpClient(new FakeHandler(Page(Order()), incomplete)))).CaptureAsync(session, sn, DateTime.Today, newPath, CancellationToken.None));
    await MustFail<BluettiAuthenticationException>(new BluettiLabelApi(new HttpClient(new FakeHandler(new JObject { ["msgCode"] = 801 }, envelope: true))).QueryAsync(session, sn, CancellationToken.None));
    await MustFail<InvalidDataException>(new BluettiLabelApi(new HttpClient(new FakeHandler(new JObject { ["wrong"] = true }))).QueryAsync(session, sn, CancellationToken.None));
    using (var cancelled = new CancellationTokenSource())
    {
        cancelled.Cancel();
        await MustFail<OperationCanceledException>(capture.CaptureAsync(session, sn, DateTime.Today, newPath, cancelled.Token));
    }
    if (!before.SequenceEqual(File.ReadAllBytes(newPath))) throw new Exception("Failed/cancelled lookup modified workbook");
    try { BluettiLabelApi.ValidateSn("SN1\nSN2"); throw new Exception("Multiple SN accepted"); } catch (ArgumentException) { }

    var wrongPath = Path.Combine(directory, "wrong.xlsx");
    File.Copy(newPath, wrongPath);
    using (var zip = ZipFile.Open(wrongPath, ZipArchiveMode.Update))
    {
        var entry = zip.GetEntry("xl/worksheets/sheet1.xml")!;
        XDocument doc; using (var stream = entry.Open()) doc = XDocument.Load(stream);
        doc.Descendants().First(e => e.Name.LocalName == "t").Value = "Wrong header";
        entry.Delete(); using var output = zip.CreateEntry("xl/worksheets/sheet1.xml").Open(); doc.Save(output);
    }
    var wrongBytes = File.ReadAllBytes(wrongPath);
    try { BluettiLabelRow.Append(wrongPath, row); throw new Exception("Wrong headers accepted"); } catch (InvalidDataException) { }
    if (!wrongBytes.SequenceEqual(File.ReadAllBytes(wrongPath))) throw new Exception("Bad template was overwritten");
    using (var locked = new FileStream(newPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
    {
        try { BluettiLabelRow.Append(newPath, row); throw new Exception("Locked workbook written"); } catch (IOException) { }
    }
    if (!before.SequenceEqual(File.ReadAllBytes(newPath))) throw new Exception("Locked workbook changed");
    Console.WriteLine("PASS: API request contract, exact SN matching, address mapping, single lookup creates/appends one row, miss leaves files untouched, ambiguous/incomplete/auth/cancel errors skip writes, locked/header-invalid files preserved, 16-column XLSX order and text safety.");
}
finally { Directory.Delete(directory, true); }

static XElement[] ReadRows(string path)
{
    using var zip = ZipFile.OpenRead(path); using var stream = zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();
    return XDocument.Load(stream).Descendants().Where(e => e.Name.LocalName == "row").ToArray();
}
static async Task MustFail<T>(Task task) where T : Exception
{
    try { await task; } catch (T) { return; }
    throw new Exception("Expected " + typeof(T).Name);
}
sealed class FakeHandler : HttpMessageHandler
{
    private readonly Queue<JObject> _responses;
    private readonly bool _envelope;
    public List<(string Method, string Path, string Query, string Body, string Auth, string Resource)> Requests { get; } = new();
    public FakeHandler(params JObject[] responses) { _responses = new Queue<JObject>(responses); }
    public FakeHandler(JObject response, bool envelope) : this(response) { _envelope = envelope; }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Requests.Add((request.Method.Method, request.RequestUri!.AbsolutePath, request.RequestUri.Query, request.Content == null ? "" : await request.Content.ReadAsStringAsync(cancellationToken), request.Headers.GetValues("Authorization").Single(), request.Headers.GetValues("x-app-resource").Single()));
        var data = _responses.Dequeue();
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent((_envelope ? data : new JObject { ["msgCode"] = 0, ["data"] = data }).ToString(), Encoding.UTF8, "application/json") };
    }
}

sealed class ShapeHandler : DelegatingHandler
{
    public ShapeHandler() : base(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false }) { }
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var response = await base.SendAsync(request, cancellationToken);
        var envelope = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        static string Shape(JToken value) => value is JObject obj ? "Object{" + string.Join(",", obj.Properties().Select(p => p.Name + ":" + p.Value.Type)) + "}" : value is JArray array ? "Array(" + array.Count + ")" : value?.Type.ToString() ?? "missing";
        Console.WriteLine("Response schema: " + request.RequestUri!.AbsolutePath + " " + Shape(envelope["data"]));
        if (envelope["data"]?["content"] is JArray content && content.First is JObject first)
        {
            Console.WriteLine("Order schema: " + Shape(first));
            if (first["productMaterialVO"] is JArray products && products.First != null) Console.WriteLine("Product schema: " + Shape(products.First));
        }
        return response;
    }
}
