using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Scanner.WPF.Services
{
    public sealed class BluettiLabelRow
    {
        public string Date { get; set; } = DateTime.Now.ToString("yyyy/MM/dd");
        public string Sn { get; set; }
        public string Sku { get; set; }
        public string Model { get; set; }
        public string Dimensions { get; set; }
        public string Weight { get; set; }
        public string Price { get; set; }
        public string Fedex { get; set; }
        public string Carton { get; set; }
        public string Rma { get; set; }
        public string WorkOrder { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
        public string Remark { get; set; }

        public static readonly string[] Headers = { "日期", "SN", "SKU", "型號", "長闊高 L x W x H (INCH)", "重量（lb）", "面單價格", "Fedex櫃號", "货箱号", "RMA编号", "工单编号", "姓名", "邮箱", "电话", "地址", "备注" };
        public string[] Values() => new[] { Date, Sn, Sku, Model, Dimensions, Weight, Price, Fedex, Carton, Rma, WorkOrder, Name, Email, Phone, Address, Remark };

        public static void Append(string path, BluettiLabelRow row)
        {
            if (row == null) throw new ArgumentNullException(nameof(row));
            if (string.IsNullOrWhiteSpace(row.Sn) || string.IsNullOrWhiteSpace(row.WorkOrder) || string.IsNullOrWhiteSpace(row.Rma)) throw new InvalidDataException("查询结果不完整，未写入 Excel。");
            if (!string.Equals(Path.GetExtension(path), ".xlsx", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("请选择 .xlsx 工作簿。");
            if (!File.Exists(path)) { Export(path, new[] { row }); return; }
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                // Deny competing writes while copying the workbook.
                using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
                using var zip = new ZipArchive(source, ZipArchiveMode.Read);
                XDocument Read(string name) { using var stream = (zip.GetEntry(name) ?? throw new InvalidDataException("Excel 文件不完整。")).Open(); return XDocument.Load(stream); }
                var workbook = Read("xl/workbook.xml");
                var sheetId = (string)workbook.Descendants(ns + "sheet").FirstOrDefault()?.Attribute(rel + "id");
                var relationship = Read("xl/_rels/workbook.xml.rels").Root.Elements().SingleOrDefault(e => (string)e.Attribute("Id") == sheetId);
                var target = (string)relationship?.Attribute("Target");
                if (string.IsNullOrWhiteSpace(target) || (string)relationship.Attribute("TargetMode") == "External") throw new InvalidDataException("无法定位 Excel 第一张工作表。");
                string sheetPath = new Uri(new Uri("https://xlsx.invalid/xl/"), target).AbsolutePath.TrimStart('/');
                if (!sheetPath.StartsWith("xl/worksheets/", StringComparison.Ordinal)) throw new InvalidDataException("不支持此工作表路径。");
                var sheet = Read(sheetPath);
                if (sheet.Root.Element(ns + "sheetProtection") != null || sheet.Root.Element(ns + "tableParts") != null) throw new InvalidDataException("请使用未保护的普通工作表，或创建新的面单 Excel 文件。");
                var data = sheet.Root.Element(ns + "sheetData") ?? throw new InvalidDataException("工作表缺少数据区。");
                var shared = zip.GetEntry("xl/sharedStrings.xml") == null ? new string[0] : Read("xl/sharedStrings.xml").Root.Elements(ns + "si").Select(e => string.Concat(e.Descendants(ns + "t").Select(t => t.Value))).ToArray();
                string CellValue(XElement cell)
                {
                    if (cell == null) return "";
                    if ((string)cell.Attribute("t") == "s") return shared[int.Parse(cell.Element(ns + "v").Value, System.Globalization.CultureInfo.InvariantCulture)];
                    return (string)cell.Attribute("t") == "inlineStr" ? string.Concat(cell.Descendants(ns + "t").Select(t => t.Value)) : (string)cell.Element(ns + "v") ?? "";
                }
                var header = data.Elements(ns + "row").SingleOrDefault(e => (int?)e.Attribute("r") == 1);
                for (int i = 0; i < Headers.Length; i++)
                    if (CellValue(header?.Elements(ns + "c").SingleOrDefault(c => (string)c.Attribute("r") == ((char)('A' + i)).ToString() + "1")) != Headers[i]) throw new InvalidDataException("Excel 前 16 列表头与面单格式不一致，未修改原文件。");
                int next = data.Elements(ns + "row").Select(e => (int?)e.Attribute("r") ?? throw new InvalidDataException("Excel 行号无效。")).DefaultIfEmpty(1).Max() + 1;
                if (next > 1048576) throw new InvalidDataException("Excel 已达到最大行数。");
                data.Add(new XElement(ns + "row", new XAttribute("r", next), row.Values().Select((value, c) => new XElement(ns + "c", new XAttribute("r", ((char)('A' + c)).ToString() + next), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value ?? ""))))));
                // Let Excel calculate the used range; extend our simple sheet filter.
                sheet.Root.Element(ns + "dimension")?.Remove();
                var filter = sheet.Root.Element(ns + "autoFilter");
                if (filter != null && ((string)filter.Attribute("ref") ?? "").StartsWith("A1:P", StringComparison.Ordinal)) filter.SetAttributeValue("ref", "A1:P" + next);
                using (var destination = ZipFile.Open(temp, ZipArchiveMode.Create))
                    foreach (var entry in zip.Entries)
                    {
                        using var output = destination.CreateEntry(entry.FullName).Open();
                        if (entry.FullName == sheetPath) sheet.Save(output);
                        else { using var input = entry.Open(); input.CopyTo(output); }
                    }
                zip.Dispose();
                source.Dispose();
                File.Move(temp, path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        // Inline strings preserve phone/SN identifiers and never execute spreadsheet formulas.
        public static void Export(string path, IEnumerable<BluettiLabelRow> rows)
        {
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
            XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
            var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var zip = ZipFile.Open(temp, ZipArchiveMode.Create))
                {
                    void Write(string name, XElement root) { using var stream = zip.CreateEntry(name).Open(); new XDocument(root).Save(stream); }
                    XElement Link(string id, string type, string target) => new XElement(pkg + "Relationship", new XAttribute("Id", id), new XAttribute("Type", rel.NamespaceName + "/" + type), new XAttribute("Target", target));
                    var data = new XElement(ns + "sheetData");
                    int index = 0;
                    foreach (var values in new[] { Headers }.Concat(rows.Select(r => r.Values())))
                    {
                        index++;
                        data.Add(new XElement(ns + "row", new XAttribute("r", index), values.Select((value, c) => new XElement(ns + "c", new XAttribute("r", ((char)('A' + c)).ToString() + index), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value ?? ""))))));
                    }
                    Write("xl/worksheets/sheet1.xml", new XElement(ns + "worksheet",
                        new XElement(ns + "sheetViews", new XElement(ns + "sheetView", new XAttribute("workbookViewId", 0), new XElement(ns + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("state", "frozen")))),
                        new XElement(ns + "cols", Enumerable.Range(1, 16).Select(c => new XElement(ns + "col", new XAttribute("min", c), new XAttribute("max", c), new XAttribute("width", c == 15 ? 65 : 25), new XAttribute("customWidth", 1)))), data,
                        new XElement(ns + "autoFilter", new XAttribute("ref", "A1:P" + index))));
                    Write("xl/workbook.xml", new XElement(ns + "workbook", new XElement(ns + "sheets", new XElement(ns + "sheet", new XAttribute("name", "面单信息"), new XAttribute("sheetId", 1), new XAttribute(rel + "id", "rId1")))));
                    Write("xl/_rels/workbook.xml.rels", new XElement(pkg + "Relationships", Link("rId1", "worksheet", "worksheets/sheet1.xml")));
                    Write("_rels/.rels", new XElement(pkg + "Relationships", Link("rId1", "officeDocument", "xl/workbook.xml")));
                    Write("[Content_Types].xml", new XElement(ct + "Types",
                        new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                        new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                        new XElement(ct + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                        new XElement(ct + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))));
                }
                File.Move(temp, path, true);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
    }
}
