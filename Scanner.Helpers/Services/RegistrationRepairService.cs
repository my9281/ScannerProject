using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Scanner.Helpers.Services
{
    public sealed class RegistrationRepairRow
    {
        public DateTime ReceivedDate { get; internal set; }
        public string Model { get; internal set; }
        public string SerialNumber { get; internal set; }
        public string TrackingNumber { get; internal set; }
        public string ReturnType { get; internal set; }
        public string Entered { get; internal set; }
        public DateTime? SystemReceivedDate { get; internal set; }
        public string ReferenceNumber { get; internal set; }
        public DateTime? ProcessingTime { get; internal set; }
    }

    public sealed class RegistrationRepairResult
    {
        public IReadOnlyList<RegistrationRepairRow> Rows { get; internal set; }
        public int FilledDateCount { get; internal set; }
        public int RemovedItemCount { get; internal set; }
        public int MissingDateCount { get; internal set; }
        public int EmptyRowCount { get; internal set; }
        public int InvalidSystemDateCount { get; internal set; }
    }

    public sealed class RegistrationWarehouseResult
    {
        public int Count { get; internal set; }
        public int UnmatchedCount { get; internal set; }
        public int OutsideMonthCount { get; internal set; }
        public int InvalidDateCount { get; internal set; }
    }

    public static class RegistrationRepairService
    {
        private static readonly XNamespace Ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace Pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
        private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";

        public static RegistrationRepairResult Load(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var book = Read(archive, "xl/workbook.xml");
                bool date1904 = new[] { "1", "true" }.Contains((string)book.Root.Element(Ns + "workbookPr")?.Attribute("date1904"));
                var strings = archive.GetEntry("xl/sharedStrings.xml") == null ? new List<string>() : Read(archive, "xl/sharedStrings.xml")
                    .Descendants(Ns + "si").Select(x => string.Concat(x.Descendants(Ns + "t").Select(t => t.Value))).ToList();
                var relationships = Read(archive, "xl/_rels/workbook.xml.rels").Root.Elements().ToDictionary(x => (string)x.Attribute("Id"), x => (string)x.Attribute("Target"));
                var result = new RegistrationRepairResult();
                var output = new List<RegistrationRepairRow>();
                int sheetCount = 0;
                foreach (var sheet in book.Descendants(Ns + "sheet"))
                {
                    string name = (string)sheet.Attribute("name") ?? "";
                    bool amazon = name.Contains("亚马逊") || name.IndexOf("amazon", StringComparison.OrdinalIgnoreCase) >= 0;
                    bool ebay = name.IndexOf("ebay", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!amazon && !ebay) continue;
                    string target = relationships[(string)sheet.Attribute(Rel + "id")].Replace('\\', '/').TrimStart('/');
                    var worksheet = Read(archive, target.StartsWith("xl/") ? target : "xl/" + target);
                    var rows = worksheet.Descendants(Ns + "sheetData").Elements(Ns + "row").ToList();
                    var header = rows.FirstOrDefault(row => row.Elements(Ns + "c").Any(cell => Column(cell) == 1 && Value(cell, strings).Trim() == "仓库收件日期"));
                    if (header == null) continue; // For example, the Amazon warehouse ID lookup is not a register.
                    var headers = header.Elements(Ns + "c").ToDictionary(Column, c => Value(c, strings).Trim());
                    if (!headers.ContainsKey(2) || headers[2] != "机型" || !headers.ContainsKey(3) || !headers[3].Equals("SN", StringComparison.OrdinalIgnoreCase) || !headers.ContainsKey(4) || headers[4] != "物流单号")
                        throw new InvalidDataException(name + " 的 A—D 列应为仓库收件日期、机型、SN、物流单号。");
                    sheetCount++;
                    // Expand vertically merged reference numbers before flattening the register.
                    var referenceCells = rows.SelectMany(r => r.Elements(Ns + "c")).Where(c => Column(c) == 8).ToDictionary(c => (string)c.Attribute("r"));
                    var mergedReferences = new Dictionary<int, string>();
                    foreach (var merge in worksheet.Descendants(Ns + "mergeCell"))
                    {
                        var match = Regex.Match((string)merge.Attribute("ref") ?? "", @"^H(?<first>\d+):H(?<last>\d+)$");
                        if (!match.Success) continue;
                        int firstRow = int.Parse(match.Groups["first"].Value), lastRow = int.Parse(match.Groups["last"].Value);
                        if (!referenceCells.TryGetValue("H" + firstRow, out var anchor)) continue;
                        for (int index = firstRow; index <= lastRow; index++) mergedReferences[index] = Value(anchor, strings).Trim();
                    }
                    DateTime? lastDate = null; // Never inherit a date from another sheet.
                    foreach (var row in rows.SkipWhile(r => r != header).Skip(1))
                    {
                        var cells = row.Elements(Ns + "c").ToDictionary(Column);
                        Func<int, string> text = column => cells.TryGetValue(column, out var cell) ? Value(cell, strings).Trim() : "";
                        DateTime date;
                        bool hasDate = TryDate(cells.ContainsKey(1) ? cells[1] : null, strings, date1904, out date);
                        if (hasDate) lastDate = date;
                        // Dates and standalone notes may precede real records, but are not themselves records.
                        if (string.IsNullOrWhiteSpace(text(2)) && string.IsNullOrWhiteSpace(text(3)) && string.IsNullOrWhiteSpace(text(4))) { result.EmptyRowCount++; continue; }
                        string model = NormalizeModel(text(2));
                        if (!Regex.IsMatch(model, @"^[A-Z0-9]+$") && text(6) == "否") { result.RemovedItemCount++; continue; }
                        if (!lastDate.HasValue) { result.MissingDateCount++; continue; }
                        if (!hasDate) result.FilledDateCount++;
                        DateTime systemDate;
                        DateTime? systemReceivedDate = null;
                        if (TryDate(cells.ContainsKey(7) ? cells[7] : null, strings, date1904, out systemDate)) systemReceivedDate = systemDate;
                        else if (text(7).Length > 0) result.InvalidSystemDateCount++;
                        string returnType = text(5);
                        if (returnType.Equals("ebay", StringComparison.OrdinalIgnoreCase)) returnType = "eBay";
                        else if (returnType == "AM" || returnType.Equals("amazon", StringComparison.OrdinalIgnoreCase)) returnType = "亚马逊退件";
                        if (returnType.Length == 0) returnType = amazon ? "亚马逊退件" : "eBay";
                        string reference = text(8);
                        if (reference.Length == 0 && mergedReferences.TryGetValue((int)row.Attribute("r"), out var mergedReference)) reference = mergedReference;
                        output.Add(new RegistrationRepairRow
                        {
                            ReceivedDate = lastDate.Value, Model = model, SerialNumber = text(3), TrackingNumber = text(4), ReturnType = returnType,
                            Entered = text(6), SystemReceivedDate = systemReceivedDate, ReferenceNumber = reference
                        });
                    }
                }
                if (sheetCount == 0) throw new InvalidDataException("未找到包含“仓库收件日期、机型、SN、物流单号”的亚马逊或 eBay 收件登记工作表。");
                result.Rows = output.AsReadOnly();
                return result;
            }
        }

        private static string NormalizeModel(string value) => Regex.Replace(value.ToUpperInvariant(), @"\s+", "");
        private static bool TryDate(XElement cell, List<string> strings, bool date1904, out DateTime date)
        {
            date = default(DateTime);
            if (cell == null) return false;
            string text = Value(cell, strings).Trim();
            string type = (string)cell.Attribute("t");
            double number;
            if ((type == null || type == "n") && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number))
            {
                try { if (number < 1) return false; date = DateTime.FromOADate(number + (date1904 ? 1462 : 0)).Date; return true; }
                catch (ArgumentException) { return false; }
            }
            // Keep an explicit date when a color or other note follows it; never extract dates from a note's middle.
            var match = Regex.Match(text, @"^(?<y>\d{4})[./年-](?<m>\d{1,2})[./月-](?<d>\d{1,2})(?!\d)");
            if (!match.Success) return false;
            try { date = new DateTime(int.Parse(match.Groups["y"].Value), int.Parse(match.Groups["m"].Value), int.Parse(match.Groups["d"].Value)); return true; }
            catch (ArgumentOutOfRangeException) { return false; }
        }

        public static RegistrationWarehouseResult ExportWarehouseInspection(RegistrationRepairResult repaired, MonthlyReportSource source, int year, int month, string path)
        {
            if (repaired == null) throw new InvalidOperationException("请先修复登记表。");
            if (source == null) throw new InvalidOperationException("请先导入完整基础表。");
            var selectedMonth = new DateTime(year, month, 1);
            if (source.TimeColumn == 0) throw new InvalidDataException("基础表缺少“处理时间”列。");
            var snHeader = source.Sheet.Element(Ns + "sheetData").Elements(Ns + "row").Where(r => (int)r.Attribute("r") <= 2)
                .SelectMany(r => r.Elements(Ns + "c")).FirstOrDefault(c => MonthlyReportSource.Value(c).Trim().Equals("sn", StringComparison.OrdinalIgnoreCase));
            if (snHeader == null) throw new InvalidDataException("基础表缺少 SN 列。");
            int snColumn = MonthlyReportSource.Column(snHeader);
            Func<XElement, string> sn = row => MonthlyReportSource.Value(row.Elements(Ns + "c").FirstOrDefault(c => MonthlyReportSource.Column(c) == snColumn)).Trim();
            var bySn = source.Rows.Where(r => sn(r).Length > 0).ToLookup(sn, StringComparer.OrdinalIgnoreCase);
            var output = new List<RegistrationRepairRow>();
            var summary = new RegistrationWarehouseResult();
            foreach (var item in repaired.Rows)
            {
                string key = (item.SerialNumber ?? "").Trim();
                var matches = key.Length == 0 ? new List<XElement>() : bySn[key].ToList();
                if (matches.Count == 0) { summary.UnmatchedCount++; continue; }
                bool inMonth = false, validDate = false;
                foreach (var match in matches)
                {
                    DateTime processed;
                    if (!source.TryDate(match, out processed)) { summary.InvalidDateCount++; continue; }
                    validDate = true;
                    if (processed.Year != selectedMonth.Year || processed.Month != selectedMonth.Month) continue;
                    inMonth = true;
                    output.Add(new RegistrationRepairRow
                    {
                        ReceivedDate = item.ReceivedDate, Model = item.Model, SerialNumber = item.SerialNumber, TrackingNumber = item.TrackingNumber,
                        ReturnType = item.ReturnType, Entered = item.Entered, SystemReceivedDate = item.SystemReceivedDate,
                        ReferenceNumber = item.ReferenceNumber, ProcessingTime = processed
                    });
                }
                if (!inMonth && validDate) summary.OutsideMonthCount++;
            }
            ExportCore(new RegistrationRepairResult { Rows = output.AsReadOnly() }, path, false, true);
            summary.Count = output.Count;
            return summary;
        }

        public static void Export(RegistrationRepairResult result, string path, bool compact = false) => ExportCore(result, path, compact, false);
        private static void ExportCore(RegistrationRepairResult result, string path, bool compact, bool warehouse)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var archive = ZipFile.Open(temporary, ZipArchiveMode.Create))
                {
                    var titles = compact ? new[] { "仓库收件日期", "机型", "SN", "物流单号" } : new[] { "仓库收件日期", "机型", "SN", "物流单号", "退件类型", "是否录入BT系统", "售后系统收件日期", "调拨单号/RMA号" };
                    if (warehouse) titles = titles.Concat(new[] { "处理时间" }).ToArray();
                    var data = new XElement(Ns + "sheetData");
                    data.Add(new XElement(Ns + "row", new XAttribute("r", 1), titles.Select((title, index) => TextCell(((char)('A' + index)) + "1", title, 2))));
                    int r = 1;
                    foreach (var item in result.Rows)
                    {
                        r++;
                        var row = new XElement(Ns + "row", new XAttribute("r", r), DateCell("A" + r, item.ReceivedDate), TextCell("B" + r, item.Model), TextCell("C" + r, item.SerialNumber), TextCell("D" + r, item.TrackingNumber));
                        if (!compact)
                        {
                            row.Add(TextCell("E" + r, item.ReturnType), TextCell("F" + r, item.Entered));
                            if (item.SystemReceivedDate.HasValue) row.Add(DateCell("G" + r, item.SystemReceivedDate.Value));
                            row.Add(TextCell("H" + r, item.ReferenceNumber));
                        }
                        if (warehouse && item.ProcessingTime.HasValue) row.Add(DateCell("I" + r, item.ProcessingTime.Value, 3));
                        data.Add(row);
                    }
                    Write(archive, "xl/worksheets/sheet1.xml", new XElement(Ns + "worksheet",
                        new XElement(Ns + "sheetViews", new XElement(Ns + "sheetView", new XAttribute("workbookViewId", 0), new XElement(Ns + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("state", "frozen")))),
                        new XElement(Ns + "cols", Enumerable.Range(1, titles.Length).Select(c => new XElement(Ns + "col", new XAttribute("min", c), new XAttribute("max", c), new XAttribute("width", c == 3 || c == 4 || c == 8 ? 38 : 24), new XAttribute("customWidth", 1)))),
                        data, new XElement(Ns + "autoFilter", new XAttribute("ref", "A1:" + (char)('A' + titles.Length - 1) + r))));
                    Write(archive, "xl/styles.xml", Styles());
                    Write(archive, "xl/workbook.xml", new XElement(Ns + "workbook", new XElement(Ns + "sheets", new XElement(Ns + "sheet", new XAttribute("name", warehouse ? "仓租检测" : "登记表修复"), new XAttribute("sheetId", 1), new XAttribute(Rel + "id", "rId1")))));
                    Write(archive, "xl/_rels/workbook.xml.rels", new XElement(Pkg + "Relationships", Relationship("rId1", "worksheet", "worksheets/sheet1.xml"), Relationship("rId2", "styles", "styles.xml")));
                    Write(archive, "_rels/.rels", new XElement(Pkg + "Relationships", Relationship("rId1", "officeDocument", "xl/workbook.xml")));
                    Write(archive, "[Content_Types].xml", new XElement(Ct + "Types",
                        new XElement(Ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                        new XElement(Ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                        ContentType("xl/workbook.xml", "sheet.main"), ContentType("xl/worksheets/sheet1.xml", "worksheet"), ContentType("xl/styles.xml", "styles")));
                }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        internal static XElement Styles() => new XElement(Ns + "styleSheet",
            new XElement(Ns + "numFmts", new XAttribute("count", 2), new XElement(Ns + "numFmt", new XAttribute("numFmtId", 164), new XAttribute("formatCode", "yyyy-mm-dd")), new XElement(Ns + "numFmt", new XAttribute("numFmtId", 165), new XAttribute("formatCode", "yyyy-mm-dd hh:mm:ss"))),
            new XElement(Ns + "fonts", new XAttribute("count", 2), new XElement(Ns + "font", new XElement(Ns + "sz", new XAttribute("val", 11)), new XElement(Ns + "name", new XAttribute("val", "Calibri"))), new XElement(Ns + "font", new XElement(Ns + "b"), new XElement(Ns + "sz", new XAttribute("val", 11)), new XElement(Ns + "name", new XAttribute("val", "Calibri")))),
            new XElement(Ns + "fills", new XAttribute("count", 2), new XElement(Ns + "fill", new XElement(Ns + "patternFill", new XAttribute("patternType", "none"))), new XElement(Ns + "fill", new XElement(Ns + "patternFill", new XAttribute("patternType", "gray125")))),
            new XElement(Ns + "borders", new XAttribute("count", 1), new XElement(Ns + "border")),
            new XElement(Ns + "cellStyleXfs", new XAttribute("count", 1), Xf(0, 0)),
            new XElement(Ns + "cellXfs", new XAttribute("count", 5), Xf(0, 0), Xf(164, 0), Xf(0, 1), Xf(165, 0), Xf(2, 0)),
            new XElement(Ns + "cellStyles", new XAttribute("count", 1), new XElement(Ns + "cellStyle", new XAttribute("name", "Normal"), new XAttribute("xfId", 0), new XAttribute("builtinId", 0))));
        private static XElement Xf(int format, int font) => new XElement(Ns + "xf", new XAttribute("numFmtId", format), new XAttribute("fontId", font), new XAttribute("fillId", 0), new XAttribute("borderId", 0), new XAttribute("xfId", 0), new XAttribute("applyNumberFormat", 1));
        private static XElement TextCell(string reference, string value, int style = 0) => new XElement(Ns + "c", new XAttribute("r", reference), new XAttribute("t", "inlineStr"), new XAttribute("s", style), new XElement(Ns + "is", new XElement(Ns + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), value ?? "")));
        private static XElement DateCell(string reference, DateTime value, int style = 1) => new XElement(Ns + "c", new XAttribute("r", reference), new XAttribute("s", style), new XElement(Ns + "v", value.ToOADate().ToString(CultureInfo.InvariantCulture)));
        private static XElement Relationship(string id, string type, string target) => new XElement(Pkg + "Relationship", new XAttribute("Id", id), new XAttribute("Type", Rel.NamespaceName + "/" + type), new XAttribute("Target", target));
        private static XElement ContentType(string path, string kind) => new XElement(Ct + "Override", new XAttribute("PartName", "/" + path), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml." + kind + "+xml"));
        private static int Column(XElement cell) { int index = 0; foreach (char c in (string)cell.Attribute("r") ?? "") { if (!char.IsLetter(c)) break; index = index * 26 + char.ToUpperInvariant(c) - 'A' + 1; } return index; }
        private static string Value(XElement cell, List<string> strings) => (string)cell.Attribute("t") == "s" ? strings[int.Parse(cell.Element(Ns + "v").Value, CultureInfo.InvariantCulture)] : (string)cell.Attribute("t") == "inlineStr" ? string.Concat(cell.Descendants(Ns + "t").Select(t => t.Value)) : (string)cell.Element(Ns + "v") ?? "";
        private static XDocument Read(ZipArchive archive, string path) { var entry = archive.GetEntry(path) ?? throw new InvalidDataException("Excel 缺少文件：" + path); using (var stream = entry.Open()) return XDocument.Load(stream); }
        private static void Write(ZipArchive archive, string path, XElement root) { using (var stream = archive.CreateEntry(path).Open()) new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root).Save(stream); }
    }
}
