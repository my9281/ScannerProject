using Scanner.Helpers.Services;
using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
var folder = Path.Combine(Path.GetTempPath(), "RegistrationRepairTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
XElement Cell(string reference, string text, bool number = false) => new XElement(ns + "c", new XAttribute("r", reference), number ? null : new XAttribute("t", "inlineStr"), number ? new XElement(ns + "v", text) : new XElement(ns + "is", new XElement(ns + "t", text)));
XElement Row(int index, string date, string model, string entered = "是", bool number = false) => new XElement(ns + "row", new XAttribute("r", index), Cell("A" + index, date, number), Cell("B" + index, model), Cell("C" + index, "000123"), Cell("D" + index, "000000123456789012345678901"), Cell("E" + index, "EBAY"), Cell("F" + index, entered));
void Write(ZipArchive archive, string name, XElement root) { using var stream = archive.CreateEntry(name).Open(); new XDocument(root).Save(stream); }
XDocument Read(ZipArchive archive, string name) { using var stream = archive.GetEntry(name)!.Open(); return XDocument.Load(stream); }
void Fixture(string path, bool date1904)
{
    using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
    string[] names = { "亚马逊收件登记表2025", "eBay&无源头退货登记表2025", "Sheet1", "亚马逊-logistar ID" };
    Write(zip, "xl/workbook.xml", new XElement(ns + "workbook", new XElement(ns + "workbookPr", new XAttribute("date1904", date1904 ? "1" : "0")), new XElement(ns + "sheets", names.Select((name, i) => new XElement(ns + "sheet", new XAttribute("name", name), new XAttribute(rel + "id", "rId" + (i + 1)))))));
    Write(zip, "xl/_rels/workbook.xml.rels", new XElement(pkg + "Relationships", names.Select((_, i) => new XElement(pkg + "Relationship", new XAttribute("Id", "rId" + (i + 1)), new XAttribute("Target", "worksheets/sheet" + (i + 1) + ".xml")))));
    for (int index = 1; index <= names.Length; index++)
    {
        var data = new XElement(ns + "sheetData", new XElement(ns + "row", new XAttribute("r", 1), Cell("A1", "标题")), new XElement(ns + "row", new XAttribute("r", 2), Cell("A2", index == 4 ? "ID号" : "仓库收件日期"), Cell("B2", "机型"), Cell("C2", "SN"), Cell("D2", "物流单号")));
        if (index == 1)
        {
            data.Add(Row(3, "备注", "AC180"), Row(4, "2026.8.1", "el 100 v2"), Row(5, "黑色", "线材", "否"), Row(6, "", "线材", "是"), Row(7, "垃圾", "WIRE", "否"), Row(8, "2026.8.2黑色", "配件", "否"), Row(9, "备注", "AC300"), Row(10, "2026/08-03", "A-PWC-ABC", "否"), Row(11, "", "A-PWC-ABC", "是"));
            data.Elements().Single(r => (int)r.Attribute("r")! == 4).Add(Cell("H4", "TO-0001"));
            data.Add(new XElement(ns + "row", new XAttribute("r", 12), Cell("A12", "2026-08-04")), Row(13, "备注含2026.9.9", "EB3A"));
            data.Add(Row(14, (new DateTime(2026, 8, 5).ToOADate() - (date1904 ? 1462 : 0)).ToString(CultureInfo.InvariantCulture), "CHARGER1", "否", true));
        }
        else if (index == 2) data.Add(Row(3, "空白日期不能跨表继承", "AC180"), Row(4, "2026.9.1", "eb3a"), Row(5, "2026.2.30", "ac300"));
        else data.Add(Row(3, "2026.8.1", "SHOULD-NOT-EXPORT"));
        var sheet = new XElement(ns + "worksheet", data);
        if (index == 1) sheet.Add(new XElement(ns + "mergeCells", new XElement(ns + "mergeCell", new XAttribute("ref", "H4:H7"))));
        Write(zip, "xl/worksheets/sheet" + index + ".xml", sheet);
    }
}
try
{
    foreach (bool date1904 in new[] { false, true })
    {
        string input = Path.Combine(folder, "input" + date1904 + ".xlsx");
        Fixture(input, date1904);
        var result = RegistrationRepairService.Load(input);
        Check(result.Rows.Count == 9, "Expected retained rows under AND deletion rule");
        Check(result.RemovedItemCount == 3 && result.MissingDateCount == 2 && result.FilledDateCount == 6, "Repair counters");
        Check(result.Rows[0].Model == "EL100V2" && result.Rows[0].ReferenceNumber == "TO-0001", "Model normalization or reference");
        Check(result.Rows[1].Model == "线材" && result.Rows[1].ReferenceNumber == "TO-0001", "Nonalphanumeric with F=yes must remain and merged reference expanded");
        Check(result.Rows[2].Model == "WIRE", "Alphanumeric with F=no must remain");
        Check(result.Rows[3].ReceivedDate == new DateTime(2026, 8, 2), "Excluded row date must update date context");
        Check(result.Rows[4].ReceivedDate == new DateTime(2026, 8, 3), "Mixed separators and excluded-row date");
        Check(result.Rows[5].ReceivedDate == new DateTime(2026, 8, 4), "Date-only row and note containing date");
        Check(result.Rows[6].ReceivedDate == new DateTime(2026, 8, 5), "1904/numeric date");
        Check(result.Rows[8].ReceivedDate == new DateTime(2026, 9, 1), "Invalid date inherits previous date within same sheet");
        Check(result.Rows.All(r => r.SerialNumber == "000123" && r.TrackingNumber == "000000123456789012345678901"), "Identifiers changed");
        File.Delete(input);
        string output = Path.Combine(folder, "output.xlsx");
        RegistrationRepairService.Export(result, output);
        RegistrationRepairService.Export(result, output); // atomic overwrite
        using var zip = ZipFile.OpenRead(output);
        Check(Read(zip, "xl/workbook.xml").Descendants(ns + "sheet").Count() == 1, "One combined dataset");
        var rows = Read(zip, "xl/worksheets/sheet1.xml").Descendants(ns + "row").ToList();
        Check(rows.Count == 10 && rows[0].Elements().Count() == 8, "Output rows or fields");
        Check(rows[1].Elements().First().Attribute("t") == null && (string)rows[1].Elements().First().Attribute("s")! == "1", "Output dates must be numeric and formatted");
        Check(rows[1].Elements().Skip(2).First().Value == "000123", "Output SKU/SN precision");
        Check(!zip.Entries.Any(e => e.FullName.Contains("media") || e.FullName.Contains("drawing")), "Images must not be exported");
    }
    foreach (bool date1904 in new[] { false, true })
    {
        string basePath = Path.Combine(folder, "warehouse-base" + date1904 + ".xlsx");
        using (var zip = ZipFile.Open(basePath, ZipArchiveMode.Create))
        {
            Write(zip, "xl/workbook.xml", new XElement(ns + "workbook", new XElement(ns + "workbookPr", new XAttribute("date1904", date1904 ? "1" : "0")), new XElement(ns + "sheets", new XElement(ns + "sheet", new XAttribute("name", "基础表"), new XAttribute(rel + "id", "rId1")))));
            Write(zip, "xl/_rels/workbook.xml.rels", new XElement(pkg + "Relationships", new XElement(pkg + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Target", "worksheets/sheet1.xml"))));
            var data = new XElement(ns + "sheetData", new XElement(ns + "row", new XAttribute("r", 1), Cell("A1", "SN"), Cell("B1", "处理时间")), new XElement(ns + "row", new XAttribute("r", 2)));
            string[] sns = { "first", "first", "first", "first", "first", "first", "outside", "invalid", "" };
            string[] dates = { "2026-08-01 00:00:00", (new DateTime(2026, 8, 31, 23, 59, 59).ToOADate() - (date1904 ? 1462 : 0)).ToString(CultureInfo.InvariantCulture), "2026-07-31 23:59:59", "2026-09-01", "2025-08-01", "invalid", "2026-09-01", "", "2026-08-01" };
            for (int i = 0; i < sns.Length; i++) data.Add(new XElement(ns + "row", new XAttribute("r", i + 3), Cell("A" + (i + 3), sns[i]), Cell("B" + (i + 3), dates[i], i == 1)));
            Write(zip, "xl/worksheets/sheet1.xml", new XElement(ns + "worksheet", data));
        }
        var source = MonthlyReportSource.Load(basePath);
        File.Delete(basePath);
        var repaired = new RegistrationRepairResult { Rows = new[]
        {
            new RegistrationRepairRow { ReceivedDate = new DateTime(2025, 1, 1), SerialNumber = " FIRST ", Model = "AC180" },
            new RegistrationRepairRow { ReceivedDate = new DateTime(2026, 8, 1), SerialNumber = "outside", Model = "AC300" },
            new RegistrationRepairRow { ReceivedDate = new DateTime(2026, 8, 1), SerialNumber = "invalid", Model = "EB3A" },
            new RegistrationRepairRow { ReceivedDate = new DateTime(2026, 8, 1), SerialNumber = "missing", Model = "AC70" },
            new RegistrationRepairRow { ReceivedDate = new DateTime(2026, 8, 1), SerialNumber = "", Model = "AC2A" }
        } };
        string output = Path.Combine(folder, "warehouse.xlsx");
        var result = RegistrationRepairService.ExportWarehouseInspection(repaired, source, 2026, 8, output);
        Check(result.Count == 2 && result.UnmatchedCount == 2 && result.OutsideMonthCount == 1 && result.InvalidDateCount == 2, "Warehouse matching or processing-time month boundaries");
        using (var zip = ZipFile.OpenRead(output))
        {
            Check((string)Read(zip, "xl/workbook.xml").Descendants(ns + "sheet").Single().Attribute("name")! == "仓租检测", "Warehouse sheet name");
            var rows = Read(zip, "xl/worksheets/sheet1.xml").Descendants(ns + "row").ToList();
            Check(rows.Count == 3 && rows[0].Elements().Count() == 9 && rows[0].Elements().Last().Value == "处理时间", "Warehouse headers and rows");
            Check(rows.Skip(1).All(row => DateTime.FromOADate(double.Parse(row.Elements().First().Value, CultureInfo.InvariantCulture)).Year == 2025), "Must retain out-of-month receipt dates when processing time matches");
            Check(DateTime.FromOADate(double.Parse(rows[2].Elements().Last().Value, CultureInfo.InvariantCulture)) == new DateTime(2026, 8, 31, 23, 59, 59), "Processing timestamp or numeric/1904 date lost");
        }
        RegistrationRepairService.ExportWarehouseInspection(repaired, source, 2024, 2, output);
        using (var zip = ZipFile.OpenRead(output)) Check(Read(zip, "xl/worksheets/sheet1.xml").Descendants(ns + "row").Count() == 1, "Empty warehouse month");
        try { RegistrationRepairService.ExportWarehouseInspection(repaired, source, 2026, 13, output); throw new Exception("Invalid month accepted"); } catch (ArgumentOutOfRangeException) { }
    }
    Console.WriteLine("PASS: warehouse SN matching, processing time rather than receipt date, all duplicate-SN events, year/month boundaries, 1904/numeric dates, full timestamps, unmatched/invalid records, empty month and snapshot.");
    Console.WriteLine("PASS: per-sheet date fill, mixed dates and notes, numeric/1904 dates, normalization, exact AND deletion rule, merged reference numbers, identifier preservation, sheet scope, 8 columns, image-free combined export and overwrite.");
    if (args.Length == 2)
    {
        var result = RegistrationRepairService.Load(args[0]);
        RegistrationRepairService.Export(result, args[1]);
        Console.WriteLine($"Sample: {result.Rows.Count} retained; {result.FilledDateCount} dates filled; {result.RemovedItemCount} removed; {result.MissingDateCount} missing dates; {result.InvalidSystemDateCount} invalid system dates.");
    }
}
finally { Directory.Delete(folder, true); }
