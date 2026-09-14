using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Scanner.WPF.Helpers
{
    internal static class ScanWorkbookHelper
    {
        private static readonly XNamespace Main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

        public static void EnsureExists(string path)
        {
            if (File.Exists(path)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                Write(archive, "[Content_Types].xml", Xml("http://schemas.openxmlformats.org/package/2006/content-types", "Types",
                    Element("Default", "Extension", "rels", "ContentType", "application/vnd.openxmlformats-package.relationships+xml"),
                    Element("Default", "Extension", "xml", "ContentType", "application/xml"),
                    Element("Override", "PartName", "/xl/workbook.xml", "ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"),
                    Element("Override", "PartName", "/xl/worksheets/sheet1.xml", "ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")));
                Write(archive, "_rels/.rels", Xml("http://schemas.openxmlformats.org/package/2006/relationships", "Relationships",
                    Element("Relationship", "Id", "rId1", "Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "Target", "xl/workbook.xml")));
                XElement workbook = new XElement(Main + "workbook", new XAttribute(XNamespace.Xmlns + "r", Rel),
                    new XElement(Main + "sheets",
                        new XElement(Main + "sheet", new XAttribute("name", "扫描记录"), new XAttribute("sheetId", "1"), new XAttribute(Rel + "id", "rId1"))));
                Write(archive, "xl/workbook.xml", new XDocument(workbook));
                Write(archive, "xl/_rels/workbook.xml.rels", Xml("http://schemas.openxmlformats.org/package/2006/relationships", "Relationships",
                    Element("Relationship", "Id", "rId1", "Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", "Target", "worksheets/sheet1.xml")));
                XElement data = new XElement(Main + "sheetData", Row(1, "型号", "SN", "OID"));
                Write(archive, "xl/worksheets/sheet1.xml", new XDocument(new XElement(Main + "worksheet",
                    new XElement(Main + "sheetViews", new XElement(Main + "sheetView", new XAttribute("workbookViewId", "0"), new XElement(Main + "pane", new XAttribute("ySplit", "1"), new XAttribute("topLeftCell", "A2"), new XAttribute("state", "frozen")))),
                    new XElement(Main + "cols", Column(1, 18), Column(2, 34), Column(3, 42)), data,
                    new XElement(Main + "autoFilter", new XAttribute("ref", "A1:C1")))));
            }
        }

        public static void Append(string path, string model, string sn, string oid)
        {
            EnsureExists(path);
            using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Update))
            {
                ZipArchiveEntry entry = archive.GetEntry("xl/worksheets/sheet1.xml");
                XDocument document;
                using (Stream stream = entry.Open()) document = XDocument.Load(stream);
                XElement data = document.Root.Element(Main + "sheetData");
                XElement last = data.Elements(Main + "row").Last();
                int lastNumber = (int)last.Attribute("r");
                bool pendingOid = !string.IsNullOrEmpty(CellValue(last, "C")) && string.IsNullOrEmpty(CellValue(last, "B"));
                if (!string.IsNullOrEmpty(sn) && pendingOid)
                {
                    SetCell(last, "A" + lastNumber, model);
                    SetCell(last, "B" + lastNumber, sn);
                }
                else
                {
                    data.Add(Row(lastNumber + 1, model, sn, oid));
                }
                entry.Delete();
                Write(archive, "xl/worksheets/sheet1.xml", document);
            }
        }

        private static string CellValue(XElement row, string column) => row.Elements(Main + "c").Where(c => ((string)c.Attribute("r") ?? "").StartsWith(column, StringComparison.Ordinal)).Select(c => (string)c.Descendants(Main + "t").FirstOrDefault() ?? "").FirstOrDefault() ?? "";
        private static void SetCell(XElement row, string reference, string value)
        {
            XElement cell = row.Elements(Main + "c").FirstOrDefault(c => (string)c.Attribute("r") == reference);
            if (cell == null) { cell = Cell(reference, value); row.Add(cell); }
            else cell.ReplaceNodes(new XElement(Main + "is", new XElement(Main + "t", value ?? "")));
        }
        private static XElement Row(int number, string a, string b, string c) => new XElement(Main + "row", new XAttribute("r", number), Cell("A" + number, a), Cell("B" + number, b), Cell("C" + number, c));
        private static XElement Cell(string reference, string value) => new XElement(Main + "c", new XAttribute("r", reference), new XAttribute("t", "inlineStr"), new XElement(Main + "is", new XElement(Main + "t", value ?? "")));
        private static XElement Column(int index, int width) => new XElement(Main + "col", new XAttribute("min", index), new XAttribute("max", index), new XAttribute("width", width), new XAttribute("customWidth", "1"));
        private static XElement Element(string name, params string[] attributes) { XElement result = new XElement(name); for (int i = 0; i < attributes.Length; i += 2) result.Add(new XAttribute(attributes[i], attributes[i + 1])); return result; }
        private static XDocument Xml(string ns, string root, params XElement[] children) { XNamespace n = ns; return new XDocument(new XElement(n + root, children.Select(x => new XElement(n + x.Name.LocalName, x.Attributes())))); }
        private static void Write(ZipArchive archive, string name, XDocument document) { ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal); using (Stream stream = entry.Open()) using (StreamWriter writer = new StreamWriter(stream, new UTF8Encoding(false))) document.Save(writer); }
    }
}
