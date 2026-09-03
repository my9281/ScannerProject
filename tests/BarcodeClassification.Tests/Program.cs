using DesktopScan = Scanner.Services.ScanService;
using DesktopOid = Scanner.Services.OidService;
using MobileScan = MaUIScanner.Services.ScanService;
using MobileOid = MaUIScanner.Services.OidService;

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
var mobile = new MobileScan(new MobileOid(), new MaUIScanner.Services.ScanLogService());
var mobileFirst = await mobile.RecordAsync(longCode);
var mobileSecond = await mobile.RecordAsync(longCode);
Assert(mobileFirst.Oid.IsOid && !mobileFirst.Oid.ShouldPrint && mobileFirst.WasRecorded, "MAUI first OID");
Assert(mobileSecond.Oid.ShouldPrint && !mobileSecond.WasRecorded, "MAUI repeated OID");

Console.WriteLine($"PASS: {cases.Length} classification cases on desktop and MAUI; first/repeated OID scans.");

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
