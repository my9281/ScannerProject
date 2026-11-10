using Scanner.Models;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Scanner.Helpers.Services
{
    public static class OutboundInspectionXlsxWriter
    {
        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static void Write(string path, IList<OutboundInspectionRecord> records)
        {
            if (records == null || records.Count == 0) throw new InvalidOperationException("没有可导出的出库检测数据。");
            if (File.Exists(path)) File.Delete(path);
            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "[Content_Types].xml", ContentTypes());
                WriteEntry(archive, "_rels/.rels", RootRelationships());
                WriteEntry(archive, "xl/workbook.xml", Workbook());
                WriteEntry(archive, "xl/_rels/workbook.xml.rels", WorkbookRelationships());
                WriteEntry(archive, "xl/styles.xml", Styles());
                WriteEntry(archive, "xl/worksheets/sheet1.xml", Worksheet(records));
            }
        }

        private static XDocument Worksheet(IList<OutboundInspectionRecord> records)
        {
            XElement sheetData = new XElement(MainNs + "sheetData");
            sheetData.Add(Row(1, 1, "编号", "SN", "SKU", "类型", "处理方式", "处理日期"));
            for (int index = 0; index < records.Count; index++)
            {
                OutboundInspectionRecord item = records[index];
                int rowNumber = index + 2;
                XElement row = new XElement(MainNs + "row", new XAttribute("r", rowNumber), new XAttribute("ht", "25"), new XAttribute("customHeight", "1"));
                row.Add(NumberCell("A" + rowNumber, item.Number, 2));
                row.Add(TextCell("B" + rowNumber, item.Sn, 2));
                row.Add(TextCell("C" + rowNumber, item.Sku, 2));
                row.Add(TextCell("D" + rowNumber, item.Type, 2));
                row.Add(TextCell("E" + rowNumber, item.ProcessingMethod, 2));
                DateTime processingDate;
                if (DateTime.TryParse(item.ProcessingTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out processingDate)) row.Add(NumberCell("F" + rowNumber, processingDate.ToOADate(), 3));
                else row.Add(TextCell("F" + rowNumber, item.ProcessingTime, 2));
                sheetData.Add(row);
            }
            int footer = records.Count + 4;
            sheetData.Add(FooterRow(footer, "库位：", string.Empty));
            sheetData.Add(FooterRow(footer + 1, "签章：", string.Empty));
            sheetData.Add(FooterRow(footer + 2, "日期：", DateTime.Now.ToString("MMM d yyyy", CultureInfo.GetCultureInfo("en-US"))));
            XElement worksheet = new XElement(MainNs + "worksheet",
                new XAttribute(XNamespace.Xmlns + "r", RelationshipNs),
                new XElement(MainNs + "sheetViews", new XElement(MainNs + "sheetView", new XAttribute("workbookViewId", "0"), new XElement(MainNs + "pane", new XAttribute("ySplit", "1"), new XAttribute("topLeftCell", "A2"), new XAttribute("state", "frozen")))),
                new XElement(MainNs + "sheetFormatPr", new XAttribute("defaultRowHeight", "15")),
                new XElement(MainNs + "cols",
                    Column(1, 8), Column(2, 25), Column(3, 31), Column(4, 16), Column(5, 16), Column(6, 17)),
                sheetData,
                new XElement(MainNs + "pageMargins", new XAttribute("left", "0.25"), new XAttribute("right", "0.25"), new XAttribute("top", "0.35"), new XAttribute("bottom", "0.35"), new XAttribute("header", "0"), new XAttribute("footer", "0")),
                new XElement(MainNs + "pageSetup", new XAttribute("orientation", "landscape"), new XAttribute("fitToWidth", "1"), new XAttribute("fitToHeight", "0")));
            foreach (XElement row in sheetData.Elements(MainNs + "row"))
            {
                int rowIndex = (int)row.Attribute("r");
                foreach (XElement cell in row.Elements(MainNs + "c"))
                {
                    string reference = (string)cell.Attribute("r");
                    if (reference.StartsWith("D") || reference.StartsWith("E"))
                        cell.SetAttributeValue("s", rowIndex >= footer ? 5 : 4);
                }
            }
            FitContent(worksheet, sheetData);
            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), worksheet);
        }

        // Excel stores calculated dimensions; bestFit alone does not resize on open.
        private static void FitContent(XElement worksheet, XElement sheetData)
        {
            double[] widths = new double[6];
            foreach (XElement cell in sheetData.Descendants(MainNs + "c"))
            {
                int column = ((string)cell.Attribute("r"))[0] - 'A';
                string value = DisplayText(cell);
                double length = value.Split('\n').Max(line => line.Sum(c => c > 255 ? 2.0 : 1.0));
                widths[column] = Math.Max(widths[column], Math.Min(60, Math.Max(10, length * 1.4 + 3)));
            }
            worksheet.Element(MainNs + "cols").ReplaceNodes(widths.Select((width, index) => Column(index + 1, width)));
            foreach (XElement row in sheetData.Elements(MainNs + "row"))
            {
                int lines = 1;
                foreach (XElement cell in row.Elements(MainNs + "c"))
                {
                    int column = ((string)cell.Attribute("r"))[0] - 'A';
                    int count = DisplayText(cell).Split('\n').Sum(line =>
                        Math.Max(1, (int)Math.Ceiling(line.Sum(c => c > 255 ? 2.0 : 1.0) * 1.4 / Math.Max(1, widths[column] - 3))));
                    lines = Math.Max(lines, count);
                }
                row.SetAttributeValue("ht", Math.Min(409, 24 * lines + 4));
                row.SetAttributeValue("customHeight", "1");
            }
        }

        private static string DisplayText(XElement cell)
        {
            if ((string)cell.Attribute("s") == "3") return "12月31日";
            return cell.Element(MainNs + "is") != null
                ? string.Concat(cell.Descendants(MainNs + "t").Select(t => t.Value))
                : (string)cell.Element(MainNs + "v") ?? string.Empty;
        }

        private static XElement Row(int rowNumber, int style, params string[] values)
        {
            XElement row = new XElement(MainNs + "row", new XAttribute("r", rowNumber), new XAttribute("ht", "25"), new XAttribute("customHeight", "1"));
            for (int index = 0; index < values.Length; index++) row.Add(TextCell(ColumnName(index + 1) + rowNumber, values[index], style));
            return row;
        }

        private static XElement FooterRow(int rowNumber, string label, string value)
        {
            return new XElement(MainNs + "row", new XAttribute("r", rowNumber), TextCell("D" + rowNumber, label, 0), TextCell("E" + rowNumber, value, 0));
        }

        private static XElement TextCell(string reference, string value, int style)
        {
            return new XElement(MainNs + "c", new XAttribute("r", reference), new XAttribute("s", style), new XAttribute("t", "inlineStr"), new XElement(MainNs + "is", new XElement(MainNs + "t", value ?? string.Empty)));
        }

        private static XElement NumberCell(string reference, double value, int style)
        {
            return new XElement(MainNs + "c", new XAttribute("r", reference), new XAttribute("s", style), new XElement(MainNs + "v", value.ToString(CultureInfo.InvariantCulture)));
        }

        private static XElement Column(int index, double width)
        {
            return new XElement(MainNs + "col", new XAttribute("min", index), new XAttribute("max", index), new XAttribute("width", width), new XAttribute("customWidth", "1"));
        }

        private static string ColumnName(int index)
        {
            string result = string.Empty;
            while (index > 0) { index--; result = (char)('A' + index % 26) + result; index /= 26; }
            return result;
        }

        private static XDocument Styles()
        {
            XElement border = new XElement(MainNs + "border", new XElement(MainNs + "left", new XAttribute("style", "thin"), new XElement(MainNs + "color", new XAttribute("rgb", "FF000000"))), new XElement(MainNs + "right", new XAttribute("style", "thin"), new XElement(MainNs + "color", new XAttribute("rgb", "FF000000"))), new XElement(MainNs + "top", new XAttribute("style", "thin"), new XElement(MainNs + "color", new XAttribute("rgb", "FF000000"))), new XElement(MainNs + "bottom", new XAttribute("style", "thin"), new XElement(MainNs + "color", new XAttribute("rgb", "FF000000"))), new XElement(MainNs + "diagonal"));
            XElement styles = new XElement(MainNs + "styleSheet",
                new XElement(MainNs + "numFmts", new XAttribute("count", "1"), new XElement(MainNs + "numFmt", new XAttribute("numFmtId", "164"), new XAttribute("formatCode", "m\"月\"d\"日\";@"))),
                new XElement(MainNs + "fonts", new XAttribute("count", "2"), new XElement(MainNs + "font", new XElement(MainNs + "sz", new XAttribute("val", "11")), new XElement(MainNs + "name", new XAttribute("val", "等线"))), new XElement(MainNs + "font", new XElement(MainNs + "b"), new XElement(MainNs + "sz", new XAttribute("val", "14")), new XElement(MainNs + "name", new XAttribute("val", "等线")))),
                new XElement(MainNs + "fills", new XAttribute("count", "2"), new XElement(MainNs + "fill", new XElement(MainNs + "patternFill", new XAttribute("patternType", "none"))), new XElement(MainNs + "fill", new XElement(MainNs + "patternFill", new XAttribute("patternType", "gray125")))),
                new XElement(MainNs + "borders", new XAttribute("count", "2"), new XElement(MainNs + "border", new XElement(MainNs + "left"), new XElement(MainNs + "right"), new XElement(MainNs + "top"), new XElement(MainNs + "bottom"), new XElement(MainNs + "diagonal")), border),
                new XElement(MainNs + "cellStyleXfs", new XAttribute("count", "1"), new XElement(MainNs + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "0"), new XAttribute("fillId", "0"), new XAttribute("borderId", "0"))),
                new XElement(MainNs + "cellXfs", new XAttribute("count", "4"),
                    new XElement(MainNs + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "0"), new XAttribute("fillId", "0"), new XAttribute("borderId", "0"), new XAttribute("xfId", "0")),
                    new XElement(MainNs + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "1"), new XAttribute("fillId", "0"), new XAttribute("borderId", "1"), new XAttribute("xfId", "0"), new XAttribute("applyAlignment", "1"), new XElement(MainNs + "alignment", new XAttribute("horizontal", "center"), new XAttribute("vertical", "center"))),
                    new XElement(MainNs + "xf", new XAttribute("numFmtId", "0"), new XAttribute("fontId", "1"), new XAttribute("fillId", "0"), new XAttribute("borderId", "1"), new XAttribute("xfId", "0"), new XAttribute("applyAlignment", "1"), new XElement(MainNs + "alignment", new XAttribute("vertical", "center"))),
                    new XElement(MainNs + "xf", new XAttribute("numFmtId", "164"), new XAttribute("fontId", "1"), new XAttribute("fillId", "0"), new XAttribute("borderId", "1"), new XAttribute("xfId", "0"), new XAttribute("applyNumberFormat", "1"), new XAttribute("applyAlignment", "1"), new XElement(MainNs + "alignment", new XAttribute("horizontal", "center"), new XAttribute("vertical", "center")))),
                new XElement(MainNs + "cellStyles", new XAttribute("count", "1"), new XElement(MainNs + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", "0"), new XAttribute("builtinId", "0"))));
            XElement fonts = styles.Element(MainNs + "fonts");
            foreach (XElement font in fonts.Elements())
            {
                font.Element(MainNs + "sz").SetAttributeValue("val", "14");
                font.Element(MainNs + "name").SetAttributeValue("val", "I.Ming");
                if (font.Element(MainNs + "b") == null) font.AddFirst(new XElement(MainNs + "b"));
            }
            fonts.Add(new XElement(MainNs + "font", new XElement(MainNs + "b"),
                new XElement(MainNs + "sz", new XAttribute("val", "14")),
                new XElement(MainNs + "name", new XAttribute("val", "字魂瘦金体"))));
            fonts.SetAttributeValue("count", "3");
            XElement xfs = styles.Element(MainNs + "cellXfs");
            XElement shoujin = new XElement(xfs.Elements().ElementAt(2));
            shoujin.SetAttributeValue("fontId", "2");
            xfs.Add(shoujin);
            XElement footerStyle = new XElement(shoujin);
            footerStyle.SetAttributeValue("borderId", "0");
            xfs.Add(footerStyle);
            xfs.SetAttributeValue("count", "6");
            foreach (XElement xf in xfs.Elements())
            {
                XElement alignment = xf.Element(MainNs + "alignment");
                if (alignment == null) { alignment = new XElement(MainNs + "alignment"); xf.Add(alignment); }
                alignment.SetAttributeValue("wrapText", "1");
                alignment.SetAttributeValue("vertical", "center");
                xf.SetAttributeValue("applyAlignment", "1");
            }
            return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), styles);
        }

        private static XDocument ContentTypes() { return Xml("http://schemas.openxmlformats.org/package/2006/content-types", new XElement("Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement("Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")), new XElement("Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")), new XElement("Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")), new XElement("Override", new XAttribute("PartName", "/xl/styles.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"))); }
        private static XDocument RootRelationships() { return Xml("http://schemas.openxmlformats.org/package/2006/relationships", new XElement("Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))); }
        private static XDocument Workbook() { return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(MainNs + "workbook", new XAttribute(XNamespace.Xmlns + "r", RelationshipNs), new XElement(MainNs + "sheets", new XElement(MainNs + "sheet", new XAttribute("name", "出库检测"), new XAttribute("sheetId", "1"), new XAttribute(RelationshipNs + "id", "rId1"))))); }
        private static XDocument WorkbookRelationships() { return Xml("http://schemas.openxmlformats.org/package/2006/relationships", new XElement("Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"), new XAttribute("Target", "worksheets/sheet1.xml")), new XElement("Relationship", new XAttribute("Id", "rId2"), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"), new XAttribute("Target", "styles.xml"))); }
        private static XDocument Xml(string ns, params XElement[] children) { XNamespace rootNs = ns; XElement[] namespaced = children.Select(child => new XElement(rootNs + child.Name.LocalName, child.Attributes(), child.Nodes())).ToArray(); return new XDocument(new XDeclaration("1.0", "UTF-8", "yes"), new XElement(rootNs + (ns.EndsWith("content-types") ? "Types" : "Relationships"), namespaced)); }
        private static void WriteEntry(ZipArchive archive, string path, XDocument document) { ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal); using (Stream stream = entry.Open()) using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false))) document.Save(writer); }
    }
}
