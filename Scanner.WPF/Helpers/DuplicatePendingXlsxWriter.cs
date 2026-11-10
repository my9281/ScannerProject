using Scanner.Models;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Scanner.WPF.Helpers
{
    internal static class DuplicatePendingXlsxWriter
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static void Write(string path, IList<InboundChecklistRecord> records, IDictionary<string, int> counts)
        {
            if (File.Exists(path)) File.Delete(path);
            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                Entry(archive, "[Content_Types].xml", Package("http://schemas.openxmlformats.org/package/2006/content-types", "Types",
                    Node("Default", "Extension", "rels", "ContentType", "application/vnd.openxmlformats-package.relationships+xml"), Node("Default", "Extension", "xml", "ContentType", "application/xml"),
                    Node("Override", "PartName", "/xl/workbook.xml", "ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"), Node("Override", "PartName", "/xl/worksheets/sheet1.xml", "ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
                Entry(archive, "_rels/.rels", Package("http://schemas.openxmlformats.org/package/2006/relationships", "Relationships", Node("Relationship", "Id", "rId1", "Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "Target", "xl/workbook.xml")));
                Entry(archive, "xl/_rels/workbook.xml.rels", Package("http://schemas.openxmlformats.org/package/2006/relationships", "Relationships", Node("Relationship", "Id", "rId1", "Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "Target", "worksheets/sheet1.xml")));
                Entry(archive, "xl/workbook.xml", new XDocument(new XElement(Main + "workbook", new XAttribute(XNamespace.Xmlns + "r", Rel), new XElement(Main + "sheets", new XElement(Main + "sheet", new XAttribute("name", "重复待检测SN"), new XAttribute("sheetId", "1"), new XAttribute(Rel + "id", "rId1"))))));
                XElement data = new XElement(Main + "sheetData", Row(1, "SN", "基础表出现次数", "类型", "SKU", "RMA编号", "检测状态", "处理日期"));
                for (int i = 0; i < records.Count; i++)
                {
                    InboundChecklistRecord item = records[i];
                    data.Add(Row(i + 2, item.Sn, counts[item.Sn.Trim()].ToString(), item.Type, item.Sku, item.RmaNumber, item.DetectionStatus, item.ProcessingTime));
                }
                XElement sheet = new XElement(Main + "worksheet", new XElement(Main + "sheetViews", new XElement(Main + "sheetView", new XAttribute("workbookViewId", "0"), new XElement(Main + "pane", new XAttribute("ySplit", "1"), new XAttribute("state", "frozen")))),
                    new XElement(Main + "cols", Col(1, 30), Col(2, 16), Col(3, 18), Col(4, 26), Col(5, 20), Col(6, 15), Col(7, 22)), data, new XElement(Main + "autoFilter", new XAttribute("ref", "A1:G" + (records.Count + 1))));
                Entry(archive, "xl/worksheets/sheet1.xml", new XDocument(sheet));
            }
        }

        private static XElement Row(int number, params string[] values) => new XElement(Main + "row", new XAttribute("r", number), values.Select((value, index) => new XElement(Main + "c", new XAttribute("r", Column(index + 1) + number), new XAttribute("t", "inlineStr"), new XElement(Main + "is", new XElement(Main + "t", value ?? string.Empty)))));
        private static XElement Col(int index, int width) => new XElement(Main + "col", new XAttribute("min", index), new XAttribute("max", index), new XAttribute("width", width), new XAttribute("customWidth", "1"));
        private static string Column(int index) { string value = ""; while (index > 0) { index--; value = (char)('A' + index % 26) + value; index /= 26; } return value; }
        private static XElement Node(string name, params string[] attributes) { XElement node = new XElement(name); for (int i = 0; i < attributes.Length; i += 2) node.Add(new XAttribute(attributes[i], attributes[i + 1])); return node; }
        private static XDocument Package(string ns, string root, params XElement[] children) { XNamespace n = ns; return new XDocument(new XElement(n + root, children.Select(x => new XElement(n + x.Name.LocalName, x.Attributes())))); }
        private static void Entry(ZipArchive archive, string name, XDocument document) { using (Stream stream = archive.CreateEntry(name, CompressionLevel.Optimal).Open()) using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false))) document.Save(writer); }
    }
}
