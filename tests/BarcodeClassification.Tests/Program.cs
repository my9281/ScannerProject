using DesktopScan = Scanner.WPF.Services.ScanService;
using DesktopOid = Scanner.WPF.Services.OidService;
using MobileScan = Scanner.MaUI.Services.ScanService;
using MobileOid = Scanner.MaUI.Services.OidService;
using Scanner.Models;
using Scanner.WPF.Services;

var cases = new (string Name, string Code, bool AreaOnly, bool Oid)[]
{
    ("null", null, false, false),
    ("blank", " \r\n", false, false),
    ("standalone sample", "420089014134", true, false),
    ("19-character area code", "420" + new string('1', 16), true, false),
    ("20-character boundary", "420" + new string('1', 17), true, false),
    ("21-character boundary", "420" + new string('1', 18), false, true),
    ("long area-prefixed OID", "420" + new string('1', 31), false, true),
    ("scanner prefix excluded from length", "]C1" + "420" + new string('1', 17), true, false),
    ("scanner prefix with long OID", "]c1" + "420" + new string('1', 18), false, true),
    ("whitespace excluded from length", "\uFEFF ]C1 420\t" + new string('1', 17) + "\r\n\u200B", true, false),
    ("whitespace with long OID", "\uFEFF ]C1 420\t" + new string('1', 18) + "\r\n\u200B", false, true),
    ("ordinary FedEx", "123456789012", false, true),
    ("ordinary 34-digit OID", new string('9', 34), false, true),
    ("UPS format", "1ZA8339B0322277594", false, true),
    ("model serial", "AC3002234000730616", false, false),
    ("420 inside serial", "AC300420123", false, false),
    ("other 21-digit code", new string('9', 21), false, false)
};

foreach (var test in cases)
{
    Assert(DesktopScan.IsGs1AreaCode(test.Code) == test.AreaOnly, "Desktop area: " + test.Name);
    Assert(DesktopOid.IsOid(test.Code) == test.Oid, "Desktop OID: " + test.Name);
    Assert(MobileScan.IsGs1AreaCode(test.Code) == test.AreaOnly, "MAUI area: " + test.Name);
    Assert(MobileOid.IsOid(test.Code) == test.Oid, "MAUI OID: " + test.Name);
}

// Exercise the record -> OID occurrence path without file writes or speech.
string longCode = "42008901413412345678901";
var desktop = new DesktopScan();
var desktopFirst = desktop.Record(longCode);
var desktopSecond = desktop.Record(longCode);
Assert(desktopFirst.Oid.IsOid && !desktopFirst.Oid.ShouldPrint && desktopFirst.WasRecorded, "Desktop first OID");
Assert(desktopSecond.Oid.ShouldPrint && !desktopSecond.WasRecorded, "Desktop repeated OID");
var mobile = new MobileScan(new MobileOid(), new Scanner.MaUI.Services.ScanLogService());
var mobileFirst = await mobile.RecordAsync(longCode);
var mobileSecond = await mobile.RecordAsync(longCode);
Assert(mobileFirst.Oid.IsOid && !mobileFirst.Oid.ShouldPrint && mobileFirst.WasRecorded, "MAUI first OID");
Assert(mobileSecond.Oid.ShouldPrint && !mobileSecond.WasRecorded, "MAUI repeated OID");

var snSearch = new WorkOrderSearchService();
snSearch.ReplaceImported(new[] { new WorkOrderRemark { Sn = "SN-001", Remark = "K列内容", IsUrgent = true } });
Assert(snSearch.Resolve(" SN-001 ", false)?.WorkOrder.Remark == "K列内容", "Urgent work order N-column SN exact match returns K-column content");

var oidSearch = new WorkOrderSearchService();
oidSearch.ReplaceImported(new[] { new WorkOrderRemark { TrackingNumber = "123456789", Remark = "OID对应K列", IsUrgent = true, IsOidRule = true } });
Assert(oidSearch.Resolve("420999123456789", true)?.WorkOrder.Remark == "OID对应K列", "Urgent work order OID suffix match returns K-column content");
var middleSearch = new WorkOrderSearchService();
middleSearch.ReplaceImported(new[] { new WorkOrderRemark { TrackingNumber = "123456789", Remark = "不应匹配", IsUrgent = true, IsOidRule = true } });
Assert(middleSearch.Resolve("420123456789999", true) == null, "OID rule must match at the tail");
var shortSearch = new WorkOrderSearchService();
shortSearch.ReplaceImported(new[] { new WorkOrderRemark { TrackingNumber = "12345678", Remark = "不应匹配", IsUrgent = true, IsOidRule = true } });
Assert(shortSearch.Resolve("42099912345678", true) == null, "O-column suffix must be longer than eight characters");

var file = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".csv");
try
{
    File.WriteAllText(file, "A,B,C,D,E,F,G,SN码,I,J,故障描述,售后处理类型,M,退货物流商,退货运单号\n" +
        "a,,,,,,,WRONG-SN,,,无法开机,退回维修,,FedEx, 878721809793\n" +
        "b,,,,,,,OTHER-SN,,,无法充电,退回换货,,FedEx,878721809793\n" +
        "c,,,,,,,WRONG-SN,,,忽略,退回维修,,UPS,123456789012\n" +
        "d,,,,,,,WRONG-SN,,,忽略,退回维修,,FedEx,   \n" +
        "e,,,,,,,WRONG-SN,,,忽略,退回维修,,FedEx,1234567\n" +
        "f,,,,,,,WRONG-SN,,,忽略,退回维修,,FedEx,12345678\n" +
        "g,,,,,,,WRONG-SN,,,九位预报,退回换货,,FedEx, 123 456 789 \n");
    var result = new Scanner.Helpers.UrgentWorkOrderImportHelper().Import(file);
    Assert(result.ImportedRowCount == 3 && result.SnRuleCount == 0 && result.OidRuleCount == 2, "FedEx only and deduplication");
    var rule = result.Rules.Single(r => r.TrackingNumber == "878721809793");
    Assert(rule.IsForecast && !rule.IsUrgent && rule.IsRepair && rule.Remark == "无法开机；无法充电", "Forecast remarks and repair");
    var search = new WorkOrderSearchService();
    search.ReplaceImported(result.Rules);
    Assert(search.Resolve("WRONG-SN", false) == null, "No source SN matching");
    Assert(search.Resolve("878721809793", true)?.WorkOrder == rule, "Tracking match");
    Assert(search.Resolve("REAL-SN", false)?.WorkOrder == rule, "Carry to SN");
    Assert(search.Resolve(new string('9', 22) + "878721809793", true)?.WorkOrder == rule, "Long barcode tail");
    Assert(search.Resolve("999878721809793", true)?.WorkOrder == rule, "Suffix match does not require 34 digits");
    Assert(search.Resolve("999123456789", true)?.WorkOrder.Remark == "九位预报", "Nine digit suffix and whitespace normalization");
    Assert(search.Resolve("123456789999", true) == null && search.Resolve("REAL-SN", false) == null, "Middle match rejected and clears context");
    Assert(search.Resolve("12345678", true) == null, "Eight digits do not trigger forecast");
    search.ReplaceImported(new[] {
        new WorkOrderRemark { IsOidRule = true, IsForecast = true, TrackingNumber = " " },
        new WorkOrderRemark { IsOidRule = true, IsForecast = true, TrackingNumber = "12345678" }
    });
    Assert(search.Resolve("99912345678", true) == null, "Blank and short rules rejected at matching layer too");
    if (args.Length > 0)
    {
        var sample = new Scanner.Helpers.UrgentWorkOrderImportHelper().Import(args[0]);
        Assert(sample.OidRuleCount > 0 && sample.Rules.All(r => r.IsForecast && string.IsNullOrEmpty(r.Sn)), "Actual sample");
        Console.WriteLine($"Sample: {sample.ImportedRowCount} rows, {sample.OidRuleCount} forecasts.");
    }
}
finally { File.Delete(file); }

Console.WriteLine($"PASS: {cases.Length} classification cases; OID occurrence and urgent work-order matching rules.");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
