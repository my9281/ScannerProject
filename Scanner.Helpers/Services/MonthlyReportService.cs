using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Scanner.Helpers.Services
{
    // Keep an independent snapshot: exports must not depend on the original file still existing.
    public sealed class MonthlyReportSource
    {
        internal static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        internal readonly byte[] Workbook;
        internal readonly XElement Sheet;
        internal readonly List<XElement> Rows;
        internal readonly int TimeColumn;
        internal readonly int StatusColumn;
        internal readonly int MaterialColumn;
        internal readonly bool Date1904;
        public int InvalidDateCount { get; private set; }
        public IReadOnlyList<int> Years { get; private set; }

        private MonthlyReportSource(byte[] workbook, bool materials = false)
        {
            Workbook = workbook;
            using (var archive = new ZipArchive(new MemoryStream(workbook), ZipArchiveMode.Read))
            {
                var book = ReadXml(archive, "xl/workbook.xml");
                Date1904 = new[] { "1", "true" }.Contains((string)book.Root.Element(Ns + "workbookPr")?.Attribute("date1904"));
                XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                var first = book.Descendants(Ns + "sheet").First();
                var relationship = ReadXml(archive, "xl/_rels/workbook.xml.rels").Root.Elements()
                    .First(x => (string)x.Attribute("Id") == (string)first.Attribute(rel + "id"));
                var target = ((string)relationship.Attribute("Target")).Replace('\\', '/').TrimStart('/');
                Sheet = ReadXml(archive, target.StartsWith("xl/") ? target : "xl/" + target).Root;
                var strings = archive.GetEntry("xl/sharedStrings.xml") == null ? new List<string>() :
                    ReadXml(archive, "xl/sharedStrings.xml").Descendants(Ns + "si")
                        .Select(x => string.Concat(x.Descendants(Ns + "t").Select(t => t.Value))).ToList();
                foreach (var cell in Sheet.Descendants(Ns + "c"))
                {
                    if ((string)cell.Attribute("t") == "s")
                    {
                        var text = strings[int.Parse(cell.Element(Ns + "v").Value, CultureInfo.InvariantCulture)];
                        cell.ReplaceNodes(new XElement(Ns + "is", new XElement(Ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), text)));
                        cell.SetAttributeValue("t", "inlineStr");
                    }
                }
                var all = Sheet.Element(Ns + "sheetData").Elements(Ns + "row").ToList();
                var headerEnd = materials ? (int)(all.FirstOrDefault(r => r.Elements(Ns + "c").Any(c => Value(c).Trim() == "物料申领明细"))?.Attribute("r") ?? throw new InvalidDataException("附加表缺少“物料申领明细”列。")) : 2;
                var headers = all.Where(x => (int)x.Attribute("r") <= headerEnd).SelectMany(x => x.Elements(Ns + "c")).ToList();
                TimeColumn = FindColumn(headers, "处理时间");
                StatusColumn = FindColumn(headers, "检测状态");
                MaterialColumn = FindColumn(headers, "物料申领明细");
                if (materials && TimeColumn == 0) throw new InvalidDataException("附加表缺少“处理时间”列。");
                Rows = all.Where(x => (int)x.Attribute("r") > headerEnd && x.Elements(Ns + "c").Any(c => !string.IsNullOrWhiteSpace(Value(c)))).ToList();
                var years = new SortedSet<int>();
                foreach (var row in Rows)
                {
                    DateTime time;
                    if (TryDate(row, out time)) years.Add(time.Year); else InvalidDateCount++;
                }
                Years = years.ToList().AsReadOnly();
            }
        }
        public static MonthlyReportSource Load(string path) => Load(path, false);
        internal static MonthlyReportSource Load(string path, bool materials)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var memory = new MemoryStream()) { input.CopyTo(memory); return new MonthlyReportSource(memory.ToArray(), materials); }
        }
        internal static XDocument ReadXml(ZipArchive archive, string path)
        {
            var entry = archive.GetEntry(path) ?? throw new InvalidDataException("Excel 缺少文件：" + path);
            using (var stream = entry.Open()) return XDocument.Load(stream);
        }
        internal static int Column(XElement cell)
        {
            int index = 0;
            foreach (char c in (string)cell.Attribute("r") ?? "") { if (!char.IsLetter(c)) break; index = index * 26 + char.ToUpperInvariant(c) - 'A' + 1; }
            return index;
        }
        private static int FindColumn(IEnumerable<XElement> headers, string name)
        {
            var cell = headers.FirstOrDefault(x => Value(x).Trim() == name);
            return cell == null ? 0 : Column(cell);
        }
        internal static string Value(XElement cell) => cell == null ? "" : (string)cell.Attribute("t") == "inlineStr"
            ? string.Concat(cell.Descendants(Ns + "t").Select(x => x.Value)) : (string)cell.Element(Ns + "v") ?? "";
        internal string Status(XElement row) => Value(row.Elements(Ns + "c").FirstOrDefault(x => Column(x) == StatusColumn)).Trim();
        internal bool TryDate(XElement row, out DateTime date)
        {
            date = default(DateTime);
            var cell = row.Elements(Ns + "c").FirstOrDefault(x => Column(x) == TimeColumn);
            var text = Value(cell).Trim();
            double serial;
            if (cell != null && ((string)cell.Attribute("t") == null || (string)cell.Attribute("t") == "n") &&
                double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out serial))
            {
                try { date = DateTime.FromOADate(serial + (Date1904 ? 1462 : 0)); return serial >= 0; } catch (ArgumentException) { return false; }
            }
            return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date) ||
                DateTime.TryParse(text, CultureInfo.GetCultureInfo("zh-CN"), DateTimeStyles.AllowWhiteSpaces, out date);
        }
        internal void Validate()
        {
            if (TimeColumn == 0 || StatusColumn == 0) throw new InvalidDataException("基础表缺少“处理时间”或“检测状态”列，请检查后重新导入。");
        }
    }

    public sealed class MonthlyMaterialSource
    {
        internal readonly MonthlyReportSource Source;
        private MonthlyMaterialSource(MonthlyReportSource source) { Source = source; }
        public int InvalidDateCount => Source.InvalidDateCount;
        public static MonthlyMaterialSource Load(string path) => new MonthlyMaterialSource(MonthlyReportSource.Load(path, true));
    }

    public sealed class MonthlyReportResult
    {
        public int Count { get; internal set; }
        public int MaterialSkuCount { get; internal set; }
        public int MaterialInvalidDateCount { get; internal set; }
        public int SheetCount { get; internal set; }
        public int InvalidDateCount { get; internal set; }
        public int UnclassifiedCount { get; internal set; }
    }

    public static class MonthlyReportService
    {
        private static readonly XNamespace Ns = MonthlyReportSource.Ns;
        private static readonly string[] Statuses = { "待检测", "检测通过", "翻新处理", "维修处理", "报废处理" };
        public static MonthlyReportResult Export(MonthlyReportSource source, int year, int month, string path, params MonthlyMaterialSource[] materialSources)
        {
            if (source == null) throw new InvalidOperationException("请重新导入完整基础表。");
            source.Validate();
            var start = new DateTime(year, month, 1);
            var rows = source.Rows.Where(row => { DateTime date; return source.TryDate(row, out date) && date.Year == start.Year && date.Month == start.Month; }).ToList();
            var names = new List<string> { "一件代发（二手）", "一件代发", month + "月数据", "待检测", "检测", "翻新", "维修", "报废", month + "月出入库" };
            var sheets = new List<XElement>
            {
                HeaderSheet(new[] { "站点名称", "NS订单号", "销售订单号", "购买渠道", "工单状态", "NS推送状态", "客户信息", "", "", "", "sku", "sn", "数量", "发货员", "发货仓库", "运单号", "运费" }, true),
                HeaderSheet(new[] { "RMA-No", "客服", "工单创建时间", "购买渠道", "自营/客供", "购买日期", "订单号", "库存SKU", "数量", "售后类型", "售后工单SN码", "售后原因", "故障描述", "客户姓名", "出库时间", "出库SKU", "出库数量", "出库产品SN码", "物流商", "运单号", "运费" }, false),
                DataSheet(source, rows)
            };
            sheets.AddRange(Statuses.Select(status => DataSheet(source, rows.Where(row => source.Status(row) == status))));
            sheets.Add(HeaderSheet(new[] { "时间", "入库", "出库" }, false));
            var totals = new SortedDictionary<string, decimal>(StringComparer.Ordinal);
            var materials = (materialSources ?? new MonthlyMaterialSource[0]).Where(x => x != null).ToList();
            foreach (var input in materials)
            {
                var materialSource = input.Source;
                foreach (var row in materialSource.Rows)
                {
                    DateTime date;
                    if (!materialSource.TryDate(row, out date) || date.Year != year || date.Month != month) continue;
                    var detail = MonthlyReportSource.Value(row.Elements(Ns + "c").FirstOrDefault(c => MonthlyReportSource.Column(c) == materialSource.MaterialColumn));
                    foreach (var entry in detail.Split(new[] { ';', '；', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        var item = entry.Trim();
                        if (item.Length == 0) continue;
                        var separator = item.LastIndexOf('*');
                        decimal quantity;
                        if (separator <= 0 || !decimal.TryParse(item.Substring(separator + 1).Trim(), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out quantity) || quantity <= 0)
                            throw new InvalidDataException("附加表第 " + (string)row.Attribute("r") + " 行物料申领明细格式无效：" + item + "。应为 SKU*数量。");
                        var sku = item.Substring(0, separator).Trim();
                        if (sku.Length == 0) throw new InvalidDataException("物料申领明细 SKU 不能为空。");
                        decimal existing;
                        totals.TryGetValue(sku, out existing);
                        totals[sku] = existing + quantity;
                    }
                }
            }
            if (materials.Count > 0)
            {
                names.Add("物料申领统计");
                var summary = HeaderSheet(new[] { "SKU", "数量" }, false);
                summary.Element(Ns + "cols").Elements().First().SetAttributeValue("width", "45");
                summary.AddFirst(new XElement(Ns + "sheetViews", new XElement(Ns + "sheetView", new XAttribute("workbookViewId", "0"), new XElement(Ns + "pane", new XAttribute("ySplit", "1"), new XAttribute("topLeftCell", "A2"), new XAttribute("state", "frozen")))));
                int index = 1;
                foreach (var total in totals)
                {
                    var row = TextRow(new[] { total.Key }, ++index);
                    row.Add(new XElement(Ns + "c", new XAttribute("r", "B" + index), new XElement(Ns + "v", total.Value.ToString(CultureInfo.InvariantCulture))));
                    summary.Element(Ns + "sheetData").Add(row);
                }
                summary.Add(new XElement(Ns + "autoFilter", new XAttribute("ref", "A1:B" + index)));
                sheets.Add(summary);
            }
            // Write next to the destination, then replace it only after the package is complete.
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
                using (var original = new ZipArchive(new MemoryStream(source.Workbook), ZipArchiveMode.Read))
                {
                    XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                    XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
                    XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
                    var types = new XElement(ct + "Types", new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")));
                    Action<string, string> contentType = (part, type) => types.Add(new XElement(ct + "Override", new XAttribute("PartName", "/" + part), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml." + type + "+xml")));
                    contentType("xl/workbook.xml", "sheet.main");
                    var links = new XElement(pkg + "Relationships");
                    var bookSheets = new XElement(Ns + "sheets");
                    for (int i = 0; i < sheets.Count; i++)
                    {
                        string id = "rId" + (i + 1), target = "worksheets/sheet" + (i + 1) + ".xml";
                        bookSheets.Add(new XElement(Ns + "sheet", new XAttribute("name", names[i]), new XAttribute("sheetId", i + 1), new XAttribute(rel + "id", id)));
                        links.Add(new XElement(pkg + "Relationship", new XAttribute("Id", id), new XAttribute("Type", rel.NamespaceName + "/worksheet"), new XAttribute("Target", target)));
                        contentType("xl/" + target, "worksheet");
                        Write(archive, "xl/" + target, sheets[i]);
                    }
                    foreach (string part in new[] { "xl/styles.xml", "xl/theme/theme1.xml" })
                    {
                        var entry = original.GetEntry(part);
                        if (entry == null) continue;
                        using (var input = entry.Open()) using (var output = archive.CreateEntry(part).Open()) input.CopyTo(output);
                        string kind = part.Contains("styles") ? "styles" : "theme";
                        links.Add(new XElement(pkg + "Relationship", new XAttribute("Id", kind), new XAttribute("Type", rel.NamespaceName + "/" + kind), new XAttribute("Target", part.Substring(3))));
                        if (kind == "styles") contentType(part, kind);
                        else types.Add(new XElement(ct + "Override", new XAttribute("PartName", "/" + part), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.theme+xml")));
                    }
                    Write(archive, "xl/workbook.xml", new XElement(Ns + "workbook", new XElement(Ns + "workbookPr", new XAttribute("date1904", source.Date1904 ? "1" : "0")), bookSheets));
                    Write(archive, "xl/_rels/workbook.xml.rels", links);
                    Write(archive, "_rels/.rels", new XElement(pkg + "Relationships", new XElement(pkg + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", rel.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
                    Write(archive, "[Content_Types].xml", types);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
            return new MonthlyReportResult { SheetCount = sheets.Count, MaterialSkuCount = totals.Count, MaterialInvalidDateCount = materials.Sum(x => x.InvalidDateCount), Count = rows.Count, InvalidDateCount = source.InvalidDateCount, UnclassifiedCount = rows.Count(row => !Statuses.Contains(source.Status(row))) };
        }

        private static XElement DataSheet(MonthlyReportSource source, IEnumerable<XElement> rows)
        {
            var data = new XElement(Ns + "sheetData");
            var headers = source.Sheet.Element(Ns + "sheetData").Elements(Ns + "row").Where(x => (int)x.Attribute("r") <= 2);
            int index = 0;
            foreach (var original in headers.Concat(rows))
            {
                var row = new XElement(original);
                row.SetAttributeValue("r", ++index);
                row.Attribute("hidden")?.Remove();
                foreach (var cell in row.Elements(Ns + "c"))
                {
                    cell.SetAttributeValue("r", ColumnName(MonthlyReportSource.Column(cell)) + index);
                    // Retain imported values; source formulas may reference rows omitted by filtering.
                    cell.Element(Ns + "f")?.Remove();
                }
                data.Add(row);
            }
            var sheet = new XElement(Ns + "worksheet");
            sheet.Add(new XElement(Ns + "sheetViews", new XElement(Ns + "sheetView", new XAttribute("workbookViewId", "0"), new XElement(Ns + "pane", new XAttribute("ySplit", "2"), new XAttribute("topLeftCell", "A3"), new XAttribute("state", "frozen")))));
            if (source.Sheet.Element(Ns + "cols") != null) sheet.Add(new XElement(source.Sheet.Element(Ns + "cols")));
            sheet.Add(data);
            var merges = source.Sheet.Element(Ns + "mergeCells")?.Elements().Where(x => ((string)x.Attribute("ref")).Split(':').All(r => int.Parse(new string(r.Where(char.IsDigit).ToArray())) <= 2)).Select(x => new XElement(x)).ToList();
            if (merges != null && merges.Count > 0) sheet.Add(new XElement(Ns + "mergeCells", new XAttribute("count", merges.Count), merges));
            return sheet;
        }
        private static XElement HeaderSheet(string[] titles, bool customer)
        {
            var data = new XElement(Ns + "sheetData", TextRow(titles, 1));
            var sheet = new XElement(Ns + "worksheet", new XElement(Ns + "cols", new XElement(Ns + "col", new XAttribute("min", "1"), new XAttribute("max", titles.Length), new XAttribute("width", "22"), new XAttribute("customWidth", "1"))), data);
            if (customer)
            {
                data.Add(TextRow(new[] { "", "", "", "", "", "", "客户名称", "客户电话", "客户邮箱", "客户地址" }, 2));
                var refs = Enumerable.Range(1, titles.Length).Where(i => i < 7 || i > 10).Select(i => ColumnName(i) + "1:" + ColumnName(i) + "2").Concat(new[] { "G1:J1" }).ToList();
                sheet.Add(new XElement(Ns + "mergeCells", new XAttribute("count", refs.Count), refs.Select(r => new XElement(Ns + "mergeCell", new XAttribute("ref", r)))));
            }
            return sheet;
        }
        private static XElement TextRow(string[] values, int row) => new XElement(Ns + "row", new XAttribute("r", row), values.Select((text, i) => new XElement(Ns + "c", new XAttribute("r", ColumnName(i + 1) + row), new XAttribute("t", "inlineStr"), new XElement(Ns + "is", new XElement(Ns + "t", text)))));
        private static string ColumnName(int column) { string name = ""; while (column > 0) { column--; name = (char)('A' + column % 26) + name; column /= 26; } return name; }
        private static void Write(ZipArchive archive, string path, XElement root) { using (var stream = archive.CreateEntry(path).Open()) new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).Save(stream); }
    }
}
