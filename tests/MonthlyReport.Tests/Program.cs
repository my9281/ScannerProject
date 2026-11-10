using Scanner.Helpers.Services;
using System.IO.Compression;
using System.Xml.Linq;

XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
var folder = Path.Combine(Path.GetTempPath(), "ScannerMonthlyTests-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(folder);
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
XElement Cell(string reference, string value, bool numeric = false) => new XElement(ns + "c", new XAttribute("r", reference), numeric ? null : new XAttribute("t", "inlineStr"), numeric ? new XElement(ns + "v", value) : new XElement(ns + "is", new XElement(ns + "t", value)));
XDocument Read(ZipArchive z, string path) { using var s = z.GetEntry(path)!.Open(); return XDocument.Load(s); }
void Fixture(string path, bool date1904 = false)
{
    using var z = ZipFile.Open(path, ZipArchiveMode.Create);
    void Write(string name, XElement root) { using var stream = z.CreateEntry(name).Open(); new XDocument(root).Save(stream); }
    XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    Write("xl/workbook.xml", new XElement(ns + "workbook", new XElement(ns + "workbookPr", new XAttribute("date1904", date1904 ? "1" : "0")), new XElement(ns + "sheets", new XElement(ns + "sheet", new XAttribute("name", "基础表"), new XAttribute(rel + "id", "rId1")))));
    XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
    Write("xl/_rels/workbook.xml.rels", new XElement(pkg + "Relationships", new XElement(pkg + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Target", "worksheets/sheet1.xml"))));
    var data = new XElement(ns + "sheetData", new XElement(ns + "row", new XAttribute("r", 1), Cell("A1", "RMA-NO"), Cell("J1", "sn"), Cell("M1", "检测状态"), Cell("O1", "处理时间"), Cell("BR1", "额外字段")), new XElement(ns + "row", new XAttribute("r", 2)));
    string[] dates = { "2026-08-01 00:00:00", "2026-08-31 23:59:59", "2026-07-31 23:59:59", "2026-09-01 00:00:00", "2025-08-01", "", "2026-08-13", "2026-08-14", "2026-08-15", "2026-08-16" };
    string[] statuses = { "待检测", "检测通过", "检测通过", "检测通过", "检测通过", "检测通过", "翻新处理", "维修处理", "报废处理", "未知状态" };
    for (int i = 0; i < dates.Length; i++)
    {
        int r = i + 3;
        bool numeric = i == 1;
        string date = numeric ? (new DateTime(2026, 8, 31, 23, 59, 59).ToOADate() - (date1904 ? 1462 : 0)).ToString(System.Globalization.CultureInfo.InvariantCulture) : dates[i];
        data.Add(new XElement(ns + "row", new XAttribute("r", r), Cell("A" + r, "RMA" + i), Cell("J" + r, i == 0 ? "" : "000123"), Cell("M" + r, statuses[i]), Cell("O" + r, date, numeric), Cell("BR" + r, "保留全部信息\n=原始文字")));
    }
    Write("xl/worksheets/sheet1.xml", new XElement(ns + "worksheet", data));
}
try
{
    foreach (bool date1904 in new[] { false, true })
    {
        string input = Path.Combine(folder, "base" + date1904 + ".xlsx");
        Fixture(input, date1904);
        var cache = new ChecklistDataCache(); cache.ImportBase(input);
        File.Delete(input); // Export must use the imported snapshot.
        string output = Path.Combine(folder, "report.xlsx");
        var result = MonthlyReportService.Export(cache.MonthlySource, 2026, 8, output);
        Check(result.Count == 6, "Month/year boundaries or missing-SN row lost");
        Check(result.InvalidDateCount == 1 && result.UnclassifiedCount == 1, "Warnings incorrect");
        using (var z = ZipFile.OpenRead(output))
        {
            var names = Read(z, "xl/workbook.xml").Descendants(ns + "sheet").Select(x => (string)x.Attribute("name")).ToArray();
            Check(names.SequenceEqual(new[] { "一件代发（二手）", "一件代发", "8月数据", "待检测", "检测", "翻新", "维修", "报废", "8月出入库" }), "Sheet names/order");
            for (int i = 1; i <= 9; i++)
            {
                var sheet = Read(z, "xl/worksheets/sheet" + i + ".xml");
                int expected = i == 1 ? 2 : i == 2 || i == 9 ? 1 : i == 3 ? 8 : 3;
                Check(sheet.Descendants(ns + "row").Count() == expected, "Wrong row count sheet " + i);
                if (i >= 3 && i <= 8) Check(sheet.Descendants(ns + "c").Any(x => (string)x.Attribute("r") == "BR3" && x.Value.Contains("保留全部信息")), "Column beyond old 64-column cap lost");
            }
        }
        MonthlyReportService.Export(cache.MonthlySource, 2024, 2, output);
        using (var z = ZipFile.OpenRead(output)) Check(Read(z, "xl/worksheets/sheet3.xml").Descendants(ns + "row").Count() == 2, "Empty month");
        try { MonthlyReportService.Export(cache.MonthlySource, 2026, 13, output); throw new Exception("Invalid month accepted"); } catch (ArgumentOutOfRangeException) { }
    }
    // The extra exports have a single header row; row 2 must be included.
    string MaterialFixture(string name, bool date1904, bool second = false, bool malformed = false)
    {
        var path = Path.Combine(folder, name + ".xlsx");
        Fixture(path, date1904);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Update);
        zip.GetEntry("xl/worksheets/sheet1.xml")!.Delete();
        var data = new XElement(ns + "sheetData", new XElement(ns + "row", new XAttribute("r", 1), Cell("A1", "处理时间"), Cell("AA1", "物料申领明细 ")));
        string[] dates = { "2026-08-01", "2026-08-31 23:59:59", "2026-09-01", "2025-08-01", "", "2026-07-31" };
        string[] details = { malformed ? "BROKEN" : second ? "000123*4;SKU-B*0.5;" : "000123*2;SKU-A*1;000123*3;", "SKU-A*2；SKU-B*1.5;", "OUTSIDE*9;", "OUTSIDE*9;", "INVALID-DATE*9;", "OUTSIDE*9;" };
        for (int j = 0; j < dates.Length; j++)
        {
            int r = j + 2;
            string date = j == 1 ? (new DateTime(2026, 8, 31, 23, 59, 59).ToOADate() - (date1904 ? 1462 : 0)).ToString(System.Globalization.CultureInfo.InvariantCulture) : dates[j];
            data.Add(new XElement(ns + "row", new XAttribute("r", r), Cell("A" + r, date, j == 1), Cell("AA" + r, details[j])));
        }
        using var stream = zip.CreateEntry("xl/worksheets/sheet1.xml").Open();
        new XDocument(new XElement(ns + "worksheet", data)).Save(stream);
        return path;
    }
    var basePath = Path.Combine(folder, "materials-base.xlsx");
    Fixture(basePath);
    var baseSource = MonthlyReportSource.Load(basePath);
    var firstPath = MaterialFixture("detection", false);
    var secondPath = MaterialFixture("maintenance", true, true);
    var firstMaterials = MonthlyMaterialSource.Load(firstPath);
    var secondMaterials = MonthlyMaterialSource.Load(secondPath);
    File.Delete(firstPath); File.Delete(secondPath);
    var materialOutput = Path.Combine(folder, "materials-report.xlsx");
    var materialResult = MonthlyReportService.Export(baseSource, 2026, 8, materialOutput, firstMaterials, secondMaterials);
    Check(materialResult.MaterialSkuCount == 3 && materialResult.MaterialInvalidDateCount == 2 && materialResult.SheetCount == 10, "Material result counts");
    Dictionary<string, decimal> ReadTotals(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var book = Read(zip, "xl/workbook.xml");
        Check((string)book.Descendants(ns + "sheet").Last().Attribute("name") == "物料申领统计", "Material sheet name");
        var rows = Read(zip, "xl/worksheets/sheet10.xml").Descendants(ns + "row").Skip(1);
        return rows.ToDictionary(row => row.Elements(ns + "c").First().Value, row => decimal.Parse(row.Elements(ns + "c").Last().Element(ns + "v")!.Value, System.Globalization.CultureInfo.InvariantCulture));
    }
    var totals = ReadTotals(materialOutput);
    Check(totals["000123"] == 9 && totals["SKU-A"] == 5 && totals["SKU-B"] == 3.5m, "Combined SKU quantities, first data row, repeats or month boundaries");
    MonthlyReportService.Export(baseSource, 2024, 2, materialOutput, firstMaterials, secondMaterials);
    Check(ReadTotals(materialOutput).Count == 0, "Empty material month must have headers only");
    MonthlyReportService.Export(baseSource, 2026, 8, materialOutput, firstMaterials);
    Check(ReadTotals(materialOutput)["000123"] == 5, "Single optional upload");
    var bytesBefore = File.ReadAllBytes(materialOutput);
    var malformedSource = MonthlyMaterialSource.Load(MaterialFixture("malformed", false, malformed: true));
    try { MonthlyReportService.Export(baseSource, 2026, 8, materialOutput, malformedSource); throw new Exception("Malformed quantity accepted"); } catch (InvalidDataException) { }
    Check(bytesBefore.SequenceEqual(File.ReadAllBytes(materialOutput)), "Invalid materials must not overwrite output");
    try { MonthlyMaterialSource.Load(basePath); throw new Exception("Missing material header accepted"); } catch (InvalidDataException) { }
    if (args.Length == 3)
    {
        var sample1 = MonthlyMaterialSource.Load(args[0]);
        var sample2 = MonthlyMaterialSource.Load(args[1]);
        var actual = MonthlyReportService.Export(baseSource, 2026, 9, args[2], sample1, sample2);
        var sampleTotals = ReadTotals(args[2]);
        Console.WriteLine($"Samples September: {actual.MaterialSkuCount} SKUs, {sampleTotals.Values.Sum()} quantity, {actual.MaterialInvalidDateCount} invalid dates.");
    }
    Console.WriteLine("PASS: optional material uploads, combined SKU totals, leading zero SKU, repeated SKU, single-row headers, numeric/1904 dates, year/month boundaries, empty month, snapshot and invalid-data protection.");
    Console.WriteLine("PASS: 9 sheets, classifications, year/month boundaries, numeric/1904 dates, invalid dates, missing SN, duplicate SN, all columns, source snapshot, empty month and overwrite.");
    if (args.Length == 2)
    {
        // The supplied example has two shipping sheets before its base-data sheet.
        var fixture = Path.Combine(folder, "sample-base.xlsx");
        File.WriteAllBytes(fixture, File.ReadAllBytes(args[0]));
        using (var z = ZipFile.Open(fixture, ZipArchiveMode.Update))
        {
            var book = Read(z, "xl/workbook.xml");
            var sheets = book.Root!.Element(ns + "sheets")!;
            var chosen = new XElement(sheets.Elements().Single(x => (string)x.Attribute("name") == "8月数据"));
            sheets.ReplaceNodes(chosen);
            z.GetEntry("xl/workbook.xml")!.Delete();
            using var output = z.CreateEntry("xl/workbook.xml").Open(); book.Save(output);
        }
        var cache = new ChecklistDataCache(); cache.ImportBase(fixture);
        var result = MonthlyReportService.Export(cache.MonthlySource, 2026, 8, args[1]);
        Console.WriteLine($"Sample export: {result.Count} records; {result.InvalidDateCount} invalid dates; {result.UnclassifiedCount} unclassified.");
    }
}
finally { Directory.Delete(folder, true); }
