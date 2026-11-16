using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Scanner.Web.Services;

static class BluettiChecks
{
    public static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool ok, string text) { if (!ok) throw new Exception(text); checks++; }
        byte[] workbook = Workbook();
        using var input = new MemoryStream(workbook);
        var rows = BluettiForecastParser.Parse(input);
        Check(rows.Count == 3, "FedEx, USPS and missing tracking records survive parsing");
        Check(rows.Any(r => r.Carrier == "usps" && r.Number == "9400111899560000000000" && r.Issue == ""), "USPS was not discarded by legacy FedEx-only behavior");
        Check(rows.Single(r => r.Carrier == "fedex").Number == "123456789012", "FedEx normalization");
        Check(rows.Single(r => r.Carrier == "fedex").SourceJson.Contains("second product"), "multi-product rows preserved");
        Check(rows.Single(r => r.Rma == "RMA-missing").Issue.Length > 0, "missing waybill is a visible data issue");
        using var second = new MemoryStream(workbook);
        Check(rows.Select(r => r.Key).SequenceEqual(BluettiForecastParser.Parse(second).Select(r => r.Key)), "repeat import identity is stable");
        using var invalid = new MemoryStream(Workbook(false));
        try { BluettiForecastParser.Parse(invalid); throw new Exception("Invalid header accepted"); } catch (InvalidDataException) { checks++; }
        var valid = new ReceivingSyncEdit(true, "user", "password", false, "site-1", 300, true, 60, 0);
        ReceivingSyncStore.Validate(valid); checks++;
        foreach (var edit in new[] { valid with { IntervalSeconds = 1 }, valid with { TrackIntervalSeconds = 0 }, valid with { ClearPassword = true }, valid with { SiteId = "" } })
        {
            try { ReceivingSyncStore.Validate(edit); throw new Exception("Invalid settings accepted"); } catch (ArgumentException) { checks++; }
        }
        var gateway = new Gateway(workbook);
        var client = new BluettiReceivingClient(new HttpClient(gateway));
        var result = await client.Fetch(" user ", "test-password", "site-1", default);
        Check(result.Count == 3 && gateway.Paths.Count == 5, "complete WPF-compatible login/site/export flow");
        Check(gateway.PasswordVerified && gateway.Paths.Last().EndsWith("exportCustomerDemands"), "password hash and export endpoint");
        var payload = BluettiReceivingClient.Payload("site-1");
        Check(payload["state"]!.GetValue<int>() == 1 && payload["type"]!.GetValue<int>() == 1 && payload["siteId"]!.ToString() == "site-1", "forecast filters match WPF");
        try { await client.Fetch("user", "test-password", "unauthorized-site", default); throw new Exception("Wrong site accepted"); }
        catch (ApiException) { checks++; }
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        try { await client.Fetch("user", "password", "site-1", cancelled.Token); throw new Exception("Cancellation ignored"); }
        catch (OperationCanceledException) { checks++; }
        return checks;
    }

    public static byte[] Workbook(bool validHeader = true)
    {
        using var memory = new MemoryStream();
        using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            void Add(string path, string xml) { using var writer = new StreamWriter(zip.CreateEntry(path).Open(), Encoding.UTF8); writer.Write(xml); }
            Add("xl/workbook.xml", """<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="DTC" sheetId="1" r:id="rId1"/></sheets></workbook>""");
            Add("xl/_rels/workbook.xml.rels", """<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Target="worksheets/sheet1.xml"/></Relationships>""");
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            string[][] data = [ ["RMA-No", validHeader ? "退货物流商" : "invalid", "退货运单号", "产品"], ["RMA-1", "FedEx", "123456789012", "first product"],
                ["RMA-1", "FedEx", "123456789012", "second product"], ["RMA-2", "USPS", "9400111899560000000000", ""], ["RMA-missing", "USPS", "", ""] ];
            var xml = new XElement(ns + "worksheet", new XElement(ns + "sheetData", data.Select((row, i) => new XElement(ns + "row", row.Select((text, j) =>
                new XElement(ns + "c", new XAttribute("r", ((char)('A' + j)).ToString() + (i + 1)), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", text))))))));
            Add("xl/worksheets/sheet1.xml", xml.ToString());
        }
        return memory.ToArray();
    }

    private sealed class Gateway(byte[] workbook) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        public bool PasswordVerified { get; private set; }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            string path = request.RequestUri!.AbsolutePath; Paths.Add(path);
            if (request.RequestUri.Host != "gwpry.bluettipower.com" || request.Headers.GetValues("x-os").Single() != "web") throw new Exception("Incorrect gateway headers");
            string json;
            if (path == "/accessToken")
            {
                string body = await request.Content!.ReadAsStringAsync(ct);
                PasswordVerified = body.Contains(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("test-password")))) && !body.Contains("test-password");
                json = """{"msgCode":0,"data":{"token":"test-token"}}""";
            }
            else
            {
                if (request.Headers.GetValues("Authorization").Single() != "test-token") throw new Exception("Wrong auth scheme");
                json = path switch
                {
                    "/api/userInfo" => """{"msgCode":0,"data":{"uid":"uid-1"}}""",
                    "/api/bluas/client/user/v1/getUserSite" => """{"msgCode":0,"data":[{"siteId":"site-1","enable":1},{"siteId":"disabled","enable":0}]}""",
                    "/api/bluas/client/role/v1/existPermissions" => """{"msgCode":0,"data":null}""",
                    _ => ""
                };
                if (path.EndsWith("existPermissions") || path.EndsWith("exportCustomerDemands"))
                    if (request.Headers.GetValues("x-app-resource").Single() != "site-1") throw new Exception("Wrong resource");
                if (path.EndsWith("exportCustomerDemands")) return new(HttpStatusCode.OK) { Content = new ByteArrayContent(workbook) };
            }
            return new(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }
}
