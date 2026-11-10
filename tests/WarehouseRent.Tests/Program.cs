using Scanner.Helpers.Services;
using System.IO.Compression;
using System.Xml.Linq;
using System.Globalization;

var folder = Path.Combine(Path.GetTempPath(), "WarehouseRentTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
void Check(bool value, string message) { if (!value) throw new Exception(message); }
XElement Cell(string reference, string value, bool numeric = false) => new XElement(ns + "c", new XAttribute("r", reference), numeric ? null : new XAttribute("t", "inlineStr"), numeric ? new XElement(ns + "v", value) : new XElement(ns + "is", new XElement(ns + "t", value)));
void Write(ZipArchive zip, string part, XElement root) { using var stream = zip.CreateEntry(part).Open(); new XDocument(root).Save(stream); }
XDocument Read(ZipArchive zip, string part) { using var stream = zip.GetEntry(part)!.Open(); return XDocument.Load(stream); }
RegistrationRepairRow Receipt(string sn, int month, int day) => new() { SerialNumber = sn, Model = "AC180", ReceivedDate = new DateTime(2026, month, day) };
WarehouseInventoryRow Inventory(string sn) => new() { Sn = sn, Sku = "000123", ShelvingDate = new DateTime(2026, 9, 1) };
try
{
    foreach (bool date1904 in new[] { false, true })
    {
        var path = Path.Combine(folder, "base" + date1904 + ".xlsx");
        using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            Write(zip, "xl/workbook.xml", new XElement(ns + "workbook", new XElement(ns + "workbookPr", new XAttribute("date1904", date1904 ? "1" : "0")), new XElement(ns + "sheets", new XElement(ns + "sheet", new XAttribute("name", "基础表"), new XAttribute(rel + "id", "rId1")))));
            Write(zip, "xl/_rels/workbook.xml.rels", new XElement(pkg + "Relationships", new XElement(pkg + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Target", "worksheets/sheet1.xml"))));
            var data = new XElement(ns + "sheetData", new XElement(ns + "row", new XAttribute("r", 1), Cell("A1", "SN"), Cell("B1", "处理时间"), Cell("C1", "检测状态"), Cell("D1", "sku")), new XElement(ns + "row", new XAttribute("r", 2)));
            string[] sns = { "old", "finished", "finished", "new-finished", "pending", "outside", "unknown", "bad-time", "multi" };
            string[] times = { "2026-09-10", "2026-09-05 23:59:59", "2026-09-08", "2026-09-20", "2026-09-10", "2026-10-01", "2026-09-15", "", "2026-09-25" };
            for (int i = 0; i < sns.Length; i++)
            {
                bool numeric = i == 1;
                string time = numeric ? (new DateTime(2026, 9, 5, 23, 59, 59).ToOADate() - (date1904 ? 1462 : 0)).ToString(CultureInfo.InvariantCulture) : times[i];
                data.Add(new XElement(ns + "row", new XAttribute("r", i + 3), Cell("A" + (i + 3), sns[i]), Cell("B" + (i + 3), time, numeric), Cell("C" + (i + 3), i == 4 ? "待检测" : "检测通过"), Cell("D" + (i + 3), "P-AC180")));
            }
            Write(zip, "xl/worksheets/sheet1.xml", new XElement(ns + "worksheet", data));
        }
        var source = MonthlyReportSource.Load(path);
        var repaired = new RegistrationRepairResult { Rows = new[] { Receipt("old", 8, 20), Receipt("new", 9, 10), Receipt("future", 10, 1), Receipt("finished", 7, 1), Receipt("new-finished", 9, 10), Receipt("pending", 8, 1), Receipt("multi", 8, 1), Receipt("multi", 9, 1), Receipt("receipt-only", 9, 12), Receipt("receipt-only", 9, 12), Receipt("", 9, 15) } };
        var inventory = new[] { Inventory(" OLD "), Inventory("old"), Inventory("new"), Inventory("future"), Inventory("unmatched"), Inventory("multi") };
        var result = WarehouseRentService.Calculate(repaired, inventory, source, 2026, 9, new DateTime(2026, 10, 5));
        Check(result.Rows.Count == 9 && result.Rows.Count(r => r.MonthDays.HasValue) == 6, "Union count, unresolved source records or duplicate inventory/receipts");
        var old = result.Rows.Single(r => r.Sn == "OLD");
        Check(old.MonthDays == 30 && old.TotalDays == 42 && old.EndDate == new DateTime(2026, 9, 30) && old.Basis.StartsWith("仍在仓"), "Stock must take precedence over September completion");
        var newer = result.Rows.Single(r => r.Sn == "NEW");
        Check(newer.MonthDays == 21 && newer.TotalDays == 21, "Mid-month inventory");
        Check(old.MonthRent == 2.40m && newer.MonthRent == 1.68m, "Monthly rent must use monthly days, with receipt-model fallback");
        var finished = result.Rows.Single(r => r.Sn == "FINISHED");
        Check(finished.MonthDays == 5 && finished.TotalDays == 67 && finished.EndDate == new DateTime(2026, 9, 5), "Completion cutoff, 1904 dates and duplicate completion deduplication");
        var newFinished = result.Rows.Single(r => r.Sn == "NEW-FINISHED");
        Check(newFinished.MonthDays == 11 && newFinished.TotalDays == 11, "Same-month completion");
        Check(result.FutureReceiptCount == 1 && result.Issues.Any(i => i.Sn == "UNMATCHED") && !result.Issues.Any(i => i.Sn == "MULTI") && result.Issues.Any(i => i.Sn == "UNKNOWN") && result.Issues.Any(i => i.Sn == "BAD-TIME"), "Future receipt, latest receipt and invalid time handling");
        Check(!result.Rows.Any(r => r.Sn == "PENDING" || r.Sn == "OUTSIDE"), "Pending/outside records must not be billed");
        var multi = result.Rows.Single(r => r.Sn == "MULTI");
        Check(multi.ReceivedDate == new DateTime(2026, 9, 1) && multi.MonthDays == 30 && multi.TotalDays == 30 && multi.ReceiptSource && multi.SystemSource && multi.InventorySource, "Latest receipt must control days and all three source flags");
        var latestOnly = new RegistrationRepairResult { Rows = new[] { Receipt("receipt-only", 9, 20), Receipt("receipt-only", 9, 2), Receipt("finished", 9, 1), Receipt("finished", 7, 1) } };
        var latestResult = WarehouseRentService.Calculate(latestOnly, Array.Empty<WarehouseInventoryRow>(), source, 2026, 9, new DateTime(2026, 10, 5));
        Check(latestResult.Rows.Single(r => r.Sn == "RECEIPT-ONLY").MonthDays == 11 && latestResult.Rows.Single(r => r.Sn == "RECEIPT-ONLY").TotalDays == 11, "Receipt-only latest date, independent of input order");
        Check(latestResult.Rows.Single(r => r.Sn == "FINISHED").TotalDays == 5, "Completed latest receipt date");
        var reentered = new RegistrationRepairResult { Rows = new[] { Receipt("old", 8, 20), Receipt("old", 10, 1) } };
        var futureResult = WarehouseRentService.Calculate(reentered, new[] { Inventory("old") }, source, 2026, 9, new DateTime(2026, 10, 5));
        Check(!futureResult.Rows.Any(r => r.Sn == "OLD") && futureResult.FutureReceiptCount == 1, "Newest receipt in next month must not fall back to an older entry");
        Check(!old.ReceiptSource && old.SystemSource && old.InventorySource, "System/inventory overlap flags");
        Check(newer.ReceiptSource && !newer.SystemSource && newer.InventorySource, "Receipt/inventory overlap flags");
        Check(newFinished.ReceiptSource && newFinished.SystemSource && !newFinished.InventorySource, "Receipt/system overlap flags");
        var onlyReceipt = result.Rows.Single(r => r.Sn == "RECEIPT-ONLY");
        Check(onlyReceipt.MonthDays == 19 && onlyReceipt.TotalDays == 19 && onlyReceipt.ReceiptSource && !onlyReceipt.SystemSource && !onlyReceipt.InventorySource, "All monthly receipts must be included, without duplicate billing");
        Check(result.Rows.Single(r => r.Sn == "UNKNOWN").SystemSource && !result.Rows.Single(r => r.Sn == "UNKNOWN").MonthDays.HasValue, "All completed system records must remain when receipt unmatched");
        Check(result.Rows.Single(r => r.Sn == "UNMATCHED").InventorySource && !result.Rows.Single(r => r.Sn == "UNMATCHED").MonthDays.HasValue, "All stock must remain when receipt unmatched");
        Check(result.Rows.Single(r => r.Sn == "").ReceiptSource && !result.Rows.Single(r => r.Sn == "").MonthDays.HasValue, "Missing SN receipt must remain for review");
        var output = Path.Combine(folder, "rent.xlsx");
        WarehouseRentService.Export(result, output);
        WarehouseRentService.Export(result, output);
        using (var zip = ZipFile.OpenRead(output))
        {
            Check(Read(zip, "xl/workbook.xml").Descendants(ns + "sheet").Count() == 2, "Bill and issues sheets");
            var exported = Read(zip, "xl/worksheets/sheet1.xml").Descendants(ns + "row").ToList();
            Check(exported.Count == 10, "Export records");
            Check(exported[0].Elements().Skip(8).Select(c => c.Value).SequenceEqual(new[] { "收货表", "系统导出", "依然在仓库", "当月仓租" }), "Exact provenance and monthly rent headers");
            var rentCell = exported.Single(r => r.Elements().First().Value == "OLD").Elements().Last();
            Check(rentCell.Attribute("t") == null && decimal.Parse(rentCell.Value, CultureInfo.InvariantCulture) == 2.40m, "Rent must export as a numeric amount");
            var unknown = exported.Single(r => r.Elements().First().Value == "UNKNOWN");
            Check(unknown.Elements().Single(c => (string)c.Attribute("r")! == "F" + (string)unknown.Attribute("r")!).Value == "", "Unknown billable days must export blank rather than zero");
            Check(!zip.Entries.Any(e => e.FullName.Contains("media")), "No images");
        }
        try { WarehouseRentService.Calculate(repaired, inventory, source, 2026, 9, new DateTime(2026, 9, 30)); throw new Exception("Early snapshot accepted"); } catch (ArgumentException) { }
        var empty = WarehouseRentService.Calculate(repaired, Array.Empty<WarehouseInventoryRow>(), source, 2024, 2, new DateTime(2024, 3, 1));
        Check(empty.Rows.Count == 0, "Empty historical month");
        var leap = WarehouseRentService.Calculate(new RegistrationRepairResult { Rows = new[] { new RegistrationRepairRow { SerialNumber = "leap", ReceivedDate = new DateTime(2024, 1, 31) } } }, new[] { new WarehouseInventoryRow { Sn = "leap", ShelvingDate = new DateTime(2024, 2, 1) } }, source, 2024, 2, new DateTime(2024, 3, 1));
        Check(leap.Rows.Single().MonthDays == 29 && leap.Rows.Single().TotalDays == 30, "Leap-year month length");
        var october = WarehouseRentService.Calculate(repaired, new[] { Inventory("old") }, source, 2026, 10, new DateTime(2026, 11, 1));
        Check(october.Rows.Single(r => r.Sn == "OLD").MonthDays == 31, "31-day month length");
        var rateRows = new RegistrationRepairResult { Rows = new[] {
            new RegistrationRepairRow { SerialNumber = "zero", Model = "PV100", ReceivedDate = new DateTime(2026, 9, 1) },
            new RegistrationRepairRow { SerialNumber = "unknown-rate", Model = "AC180P", ReceivedDate = new DateTime(2026, 9, 1) } } };
        var rated = WarehouseRentService.Calculate(rateRows, Array.Empty<WarehouseInventoryRow>(), source, 2026, 9, new DateTime(2026, 10, 5));
        Check(rated.Rows.Single(r => r.Sn == "ZERO").MonthRent == 0m, "Explicit zero rate must produce zero rent");
        Check(rated.Rows.Single(r => r.Sn == "UNKNOWN-RATE").MonthRent == null && rated.Issues.Any(i => i.Sn == "UNKNOWN-RATE"), "Unknown rates must be blank and flagged, not guessed");
        Check(WarehouseRentRates.TryGet("P-EP500-US-GY", out var epRate) && epRate == 1m && WarehouseRentRates.TryGet(" el100 v2 ", out var elRate) && elRate == .04m, "Full SKU region and normalized model lookup");
    }
    var template = Path.Combine(folder, "inventory.xlsx");
    WarehouseRentService.ExportTemplate(template);
    Check(WarehouseRentService.Import(template).Count == 0, "Empty template round trip");
    using (var zip = ZipFile.Open(template, ZipArchiveMode.Update))
    {
        var sheet = Read(zip, "xl/worksheets/sheet1.xml");
        sheet.Root!.Element(ns + "sheetData")!.Add(new XElement(ns + "row", new XAttribute("r", 2), Cell("C2", "000123"), Cell("D2", "P-AC180"), Cell("H2", new DateTime(2026, 9, 1).ToOADate().ToString(CultureInfo.InvariantCulture), true), Cell("I2", "PALLET-1")));
        zip.GetEntry("xl/worksheets/sheet1.xml")!.Delete(); Write(zip, "xl/worksheets/sheet1.xml", sheet.Root);
    }
    var imported = WarehouseRentService.Import(template).Single();
    Check(imported.Sn == "000123" && imported.Sku == "P-AC180" && imported.PalletNumber == "PALLET-1" && imported.ShelvingDate == new DateTime(2026, 9, 1), "Web schema template import and identifier precision");
    Console.WriteLine("PASS: stock precedence, 30-day September, same-month days, cumulative days, future receipts, completion settlement, pending exclusion, duplicates, ambiguous receipts, unmatched data, numeric/1904 dates, Web template import, empty months and two-sheet export.");
}
finally { Directory.Delete(folder, true); }
