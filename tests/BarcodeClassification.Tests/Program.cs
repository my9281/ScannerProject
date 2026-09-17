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

Console.WriteLine($"PASS: {cases.Length} classification cases; OID occurrence and urgent work-order matching rules.");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
