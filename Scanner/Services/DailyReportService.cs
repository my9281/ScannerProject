using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Scanner.Services
{
    public static class DailyReportService
    {
        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static string Create(DateTime today, string baseDirectory = null)
        {
            DateTime start = today.Date.AddMonths(-3);
            DateTime end = new DateTime(today.Year, today.Month, 15);
            string root = string.IsNullOrWhiteSpace(baseDirectory) ? AppDomain.CurrentDomain.BaseDirectory : baseDirectory;
            string directory = Path.Combine(root, "tempexcel");
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "日报_" + today.ToString("yyyyMMdd_HHmmssfff", CultureInfo.InvariantCulture) + ".xlsx");

            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "[Content_Types].xml", ContentTypes());
                WriteEntry(archive, "_rels/.rels", RootRelationships());
                WriteEntry(archive, "xl/workbook.xml", Workbook());
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships());
                WriteEntry(archive, "xl/styles.xml", Styles());
                WriteEntry(archive, "xl/worksheets/sheet1.xml", Worksheet(start, end));
            }
            return path;
        }

        private static XDocument Worksheet(DateTime start, DateTime end)
        {
            var dates = new List<DateTime>();
            for (DateTime date = start; date <= end; date = date.AddDays(1)) dates.Add(date);
            XElement sheetData = new XElement(MainNs + "sheetData", HeaderRow());
            for (int index = 0; index < dates.Count; index++)
            {
                int rowNumber = index + 2;
                bool banded = index % 2 == 0;
                DateTime date = dates[index];
                sheetData.Add(new XElement(MainNs + "row",
                    new XAttribute("r", rowNumber), new XAttribute("ht", "22"), new XAttribute("customHeight", "1"),
                    NumberCell("A" + rowNumber, date.ToOADate(), banded ? 3 : 2),
                    TextCell("B" + rowNumber, ChineseWeekday(date.DayOfWeek), banded ? 5 : 4),
                    TextCell("C" + rowNumber, string.Empty, banded ? 7 : 6),
                    TextCell("D" + rowNumber, string.Empty, banded ? 7 : 6)));
            }
            int lastRow = dates.Count + 1;
            XElement worksheet = new XElement(MainNs + "worksheet",
                new XAttribute(XNamespace.Xmlns + "r", RelationshipNs),
                new XElement(MainNs + "dimension", new XAttribute("ref", "A1:D" + lastRow)),
                new XElement(MainNs + "sheetViews", new XElement(MainNs + "sheetView", new XAttribute("workbookViewId", "0"),
                    new XElement(MainNs + "pane", new XAttribute("ySplit", "1"), new XAttribute("topLeftCell", "A2"), new XAttribute("activePane", "bottomLeft"), new XAttribute("state", "frozen")))),
                new XElement(MainNs + "sheetFormatPr", new XAttribute("defaultRowHeight", "22")),
                new XElement(MainNs + "cols", Column(1, 14), Column(2, 12), Column(3, 42), Column(4, 32)),
                sheetData,
                new XElement(MainNs + "autoFilter", new XAttribute("ref", "A1:D" + lastRow)),
                new XElement(MainNs + "pageMargins", new XAttribute("left", "0.3"), new XAttribute("right", "0.3"), new XAttribute("top", "0.4"), new XAttribute("bottom", "0.4"), new XAttribute("header", "0"), new XAttribute("footer", "0")));
            return Document(worksheet);
        }

        private static XElement HeaderRow()
        {
            return new XElement(MainNs + "row", new XAttribute("r", "1"), new XAttribute("ht", "24"), new XAttribute("customHeight", "1"),
                TextCell("A1", "时间", 1), TextCell("B1", "日期", 1), TextCell("C1", "内容", 1), TextCell("D1", "备注", 1));
        }

        private static string ChineseWeekday(DayOfWeek day)
        {
            string[] names = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
            return names[(int)day];
        }

        private static XElement TextCell(string reference, string value, int style)
        {
            return new XElement(MainNs + "c", new XAttribute("r", reference), new XAttribute("s", style), new XAttribute("t", "inlineStr"),
                new XElement(MainNs + "is", new XElement(MainNs + "t", value ?? string.Empty)));
        }

        private static XElement NumberCell(string reference, double value, int style)
        {
            return new XElement(MainNs + "c", new XAttribute("r", reference), new XAttribute("s", style),
                new XElement(MainNs + "v", value.ToString(CultureInfo.InvariantCulture)));
        }

        private static XElement Column(int index, double width)
        {
            return new XElement(MainNs + "col", new XAttribute("min", index), new XAttribute("max", index),
                new XAttribute("width", width), new XAttribute("customWidth", "1"));
        }

        private static XDocument Styles()
        {
            XElement bottomBorder = new XElement(MainNs + "border", new XElement(MainNs + "left"), new XElement(MainNs + "right"), new XElement(MainNs + "top"),
                new XElement(MainNs + "bottom", new XAttribute("style", "thin"), new XElement(MainNs + "color", new XAttribute("rgb", "FFD3DBE8"))), new XElement(MainNs + "diagonal"));
            XElement styles = new XElement(MainNs + "styleSheet",
                new XElement(MainNs + "numFmts", new XAttribute("count", "1"), new XElement(MainNs + "numFmt", new XAttribute("numFmtId", "164"), new XAttribute("formatCode", "yyyy-mm-dd"))),
                new XElement(MainNs + "fonts", new XAttribute("count", "2"), Font(false, "FF18243A"), Font(true, "FFFFFFFF")),
                new XElement(MainNs + "fills", new XAttribute("count", "4"),
                    Fill("none", null), Fill("gray125", null), Fill("solid", "FF2457D6"), Fill("solid", "FFE8F4FA")),
                new XElement(MainNs + "borders", new XAttribute("count", "2"),
                    new XElement(MainNs + "border", new XElement(MainNs + "left"), new XElement(MainNs + "right"), new XElement(MainNs + "top"), new XElement(MainNs + "bottom"), new XElement(MainNs + "diagonal")), bottomBorder),
                new XElement(MainNs + "cellStyleXfs", new XAttribute("count", "1"), new XElement(MainNs + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "0"), new XAttribute("fillId", "0"), new XAttribute("borderId", "0"))),
                new XElement(MainNs + "cellXfs", new XAttribute("count", "8"),
                    CellStyle(0, 0, 0, 0, false, "left"),
                    CellStyle(1, 2, 1, 0, false, "center"),
                    CellStyle(0, 0, 1, 164, true, "center"),
                    CellStyle(0, 3, 1, 164, true, "center"),
                    CellStyle(0, 0, 1, 0, false, "center"),
                    CellStyle(0, 3, 1, 0, false, "center"),
                    CellStyle(0, 0, 1, 0, false, "left"),
                    CellStyle(0, 3, 1, 0, false, "left")),
                new XElement(MainNs + "cellStyles", new XAttribute("count", "1"), new XElement(MainNs + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", "0"), new XAttribute("builtinId", "0"))));
            return Document(styles);
        }

        private static XElement Font(bool bold, string color)
        {
            return new XElement(MainNs + "font", bold ? new XElement(MainNs + "b") : null,
                new XElement(MainNs + "sz", new XAttribute("val", "10")), new XElement(MainNs + "color", new XAttribute("rgb", color)),
                new XElement(MainNs + "name", new XAttribute("val", "Microsoft YaHei")));
        }

        private static XElement Fill(string pattern, string color)
        {
            return new XElement(MainNs + "fill", new XElement(MainNs + "patternFill", new XAttribute("patternType", pattern),
                color == null ? null : new XElement(MainNs + "fgColor", new XAttribute("rgb", color)),
                color == null ? null : new XElement(MainNs + "bgColor", new XAttribute("indexed", "64"))));
        }

        private static XElement CellStyle(int font, int fill, int border, int numberFormat, bool applyNumberFormat, string horizontal)
        {
            return new XElement(MainNs + "xf", new XAttribute("numFmtId", numberFormat), new XAttribute("fontId", font), new XAttribute("fillId", fill),
                new XAttribute("borderId", border), new XAttribute("xfId", "0"), new XAttribute("applyAlignment", "1"),
                applyNumberFormat ? new XAttribute("applyNumberFormat", "1") : null,
                new XElement(MainNs + "alignment", new XAttribute("horizontal", horizontal), new XAttribute("vertical", "center")));
        }

        private static XDocument ContentTypes()
        {
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
            return Document(new XElement(ns + "Types",
                new XElement(ns + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                new XElement(ns + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")),
                new XElement(ns + "Override", new XAttribute("PartName", "/xl/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"))));
        }

        private static XDocument RootRelationships()
        {
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
            return Document(new XElement(ns + "Relationships",
                new XElement(ns + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
        }

        private static XDocument Workbook()
        {
            return Document(new XElement(MainNs + "workbook", new XAttribute(XNamespace.Xmlns + "r", RelationshipNs),
                new XElement(MainNs + "sheets", new XElement(MainNs + "sheet", new XAttribute("name", "日报"), new XAttribute("sheetId", "1"), new XAttribute(RelationshipNs + "id", "rId1")))));
        }

        private static XDocument WorkbookRelationships()
        {
            XNamespace ns = "http://schemas.openxmlformats.org/package/2006/relationships";
            return Document(new XElement(ns + "Relationships",
                new XElement(ns + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), new XAttribute("Target", "worksheets/sheet1.xml")),
                new XElement(ns + "Relationship", new XAttribute("Id", "rId2"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"), new XAttribute("Target", "styles.xml"))));
        }

        private static XDocument Document(XElement root)
        {
            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), root);
        }

        private static void WriteEntry(ZipArchive archive, string path, XDocument document)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using (Stream stream = entry.Open())
            using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false))) document.Save(writer);
        }
    }
}
