using Scanner.Services;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

internal static class Program
{
    private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

    private static int Main(string[] args)
    {
        string testRoot = args.Length == 0
            ? Path.Combine(Path.GetTempPath(), "ScannerDailyReportTests-" + Guid.NewGuid().ToString("N"))
            : args[0];
        try
        {
            string path = DailyReportService.Create(new DateTime(2026, 9, 10, 12, 30, 0), testRoot);
            Assert(File.Exists(path), "Workbook was not created");
            Assert(string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "tempexcel", StringComparison.OrdinalIgnoreCase), "Wrong output directory");
            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                XDocument sheet = Load(archive, "xl/worksheets/sheet1.xml");
                XElement[] rows = sheet.Descendants(MainNs + "row").ToArray();
                Assert(rows.Length == 99, "Expected one header plus 98 date rows");
                Assert(ReadText(rows[0], "A1") == "时间" && ReadText(rows[0], "B1") == "日期" &&
                    ReadText(rows[0], "C1") == "内容" && ReadText(rows[0], "D1") == "备注", "Wrong headers");
                Assert(DateTime.FromOADate(ReadNumber(rows[1], "A2")) == new DateTime(2026, 6, 10), "Wrong start date");
                Assert(ReadText(rows[1], "B2") == "星期三", "Wrong start weekday");
                Assert(DateTime.FromOADate(ReadNumber(rows[98], "A99")) == new DateTime(2026, 9, 15), "Wrong end date");
                Assert(ReadText(rows[98], "B99") == "星期二", "Wrong end weekday");
                Assert((string)sheet.Descendants(MainNs + "autoFilter").Single().Attribute("ref") == "A1:D99", "Wrong filter range");
                Assert((string)sheet.Descendants(MainNs + "pane").Single().Attribute("ySplit") == "1", "Header row is not frozen");
                XDocument styles = Load(archive, "xl/styles.xml");
                Assert(styles.Descendants(MainNs + "numFmt").Any(item => (string)item.Attribute("formatCode") == "yyyy-mm-dd"), "Date format missing");
            }
            Console.WriteLine("PASS: daily report 2026-06-10 through 2026-09-15, headers, weekdays, formatting and output folder.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return 1;
        }
        finally
        {
            if (args.Length == 0 && Directory.Exists(testRoot)) Directory.Delete(testRoot, true);
        }
    }

    private static XDocument Load(ZipArchive archive, string name)
    {
        using (Stream stream = archive.GetEntry(name).Open()) return XDocument.Load(stream);
    }

    private static string ReadText(XElement row, string reference)
    {
        XElement cell = row.Elements(MainNs + "c").Single(item => (string)item.Attribute("r") == reference);
        return cell.Descendants(MainNs + "t").Single().Value;
    }

    private static double ReadNumber(XElement row, string reference)
    {
        XElement cell = row.Elements(MainNs + "c").Single(item => (string)item.Attribute("r") == reference);
        return double.Parse(cell.Element(MainNs + "v").Value, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
