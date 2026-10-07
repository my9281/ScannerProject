using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Scanner.Helpers.Services
{
    // These fields correspond to the Web shelved_pallet_data table.
    public sealed class WarehouseInventoryRow
    {
        public string Sn { get; set; }
        public string Sku { get; set; }
        public string PalletNumber { get; set; }
        public DateTime? ShelvingDate { get; set; }
    }
    public sealed class WarehouseRentRow
    {
        public string Sn { get; internal set; }
        public string Sku { get; internal set; }
        public DateTime? ReceivedDate { get; internal set; }
        public DateTime? EndDate { get; internal set; }
        public DateTime? ProcessingTime { get; internal set; }
        public int? MonthDays { get; internal set; }
        public int? TotalDays { get; internal set; }
        public decimal? MonthRent { get; internal set; }
        public string Basis { get; internal set; }
        public bool ReceiptSource { get; internal set; }
        public bool SystemSource { get; internal set; }
        public bool InventorySource { get; internal set; }
    }
    public sealed class WarehouseRentIssue
    {
        public string Sn { get; internal set; }
        public string Reason { get; internal set; }
        public bool ReceiptSource { get; internal set; }
        public bool SystemSource { get; internal set; }
        public bool InventorySource { get; internal set; }
    }
    public sealed class WarehouseRentResult
    {
        public IReadOnlyList<WarehouseRentRow> Rows { get; internal set; }
        public IReadOnlyList<WarehouseRentIssue> Issues { get; internal set; }
        public int FutureReceiptCount { get; internal set; }
    }
    public static class WarehouseRentService
    {
        private static readonly XNamespace Ns = MonthlyReportSource.Ns;
        private static string Key(string sn) => (sn ?? "").Trim().ToUpperInvariant();

        public static WarehouseRentResult Calculate(RegistrationRepairResult repaired, IEnumerable<WarehouseInventoryRow> inventory,
            MonthlyReportSource source, int year, int month, DateTime snapshotDate)
        {
            if (repaired == null || inventory == null || source == null) throw new InvalidOperationException("请先导入登记表、在仓清单和基础表。");
            var start = new DateTime(year, month, 1);
            var end = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            if (snapshotDate.Date <= end) throw new ArgumentException("请使用结算月份之后的在仓清单，并正确填写清单日期。");
            if (source.TimeColumn == 0 || source.StatusColumn == 0) throw new InvalidDataException("基础表缺少处理时间或检测状态。");
            var headers = source.Sheet.Element(Ns + "sheetData").Elements(Ns + "row").Where(r => (int)r.Attribute("r") <= 2).SelectMany(r => r.Elements(Ns + "c")).ToList();
            int column(string title) => MonthlyReportSource.Column(headers.FirstOrDefault(c => MonthlyReportSource.Value(c).Trim().Equals(title, StringComparison.OrdinalIgnoreCase)) ?? new XElement(Ns + "c"));
            int snColumn = column("SN");
            int skuColumn = column("SKU");
            if (snColumn == 0) throw new InvalidDataException("基础表缺少 SN 列。");
            string value(XElement row, int col) => MonthlyReportSource.Value(row.Elements(Ns + "c").FirstOrDefault(c => MonthlyReportSource.Column(c) == col)).Trim();
            // One receipt per SN: use the latest entry known at the inventory snapshot.
            var latestReceipts = repaired.Rows.Where(r => Key(r.SerialNumber).Length > 0 && r.ReceivedDate.Date <= snapshotDate.Date)
                .GroupBy(r => Key(r.SerialNumber)).Select(g => g.OrderByDescending(r => r.ReceivedDate).First()).ToList();
            var receipts = latestReceipts.ToLookup(r => Key(r.SerialNumber));
            var baseRows = source.Rows.Where(r => Key(value(r, snColumn)).Length > 0).ToLookup(r => Key(value(r, snColumn)));
            var completedStatuses = new HashSet<string>(new[] { "检测通过", "翻新处理", "维修处理", "报废处理" });
            var completedSn = new HashSet<string>(source.Rows.Where(r => completedStatuses.Contains(source.Status(r)) && source.TryDate(r, out var t) && t.Year == year && t.Month == month).Select(r => Key(value(r, snColumn))));
            var monthlyReceipts = latestReceipts.Concat(repaired.Rows.Where(r => Key(r.SerialNumber).Length == 0))
                .Where(r => r.ReceivedDate.Year == year && r.ReceivedDate.Month == month).ToList();
            var monthlyReceiptSn = new HashSet<string>(monthlyReceipts.Select(r => Key(r.SerialNumber)));
            var rows = new List<WarehouseRentRow>();
            var issues = new List<WarehouseRentIssue>();
            var issueKeys = new HashSet<string>();
            void issue(string sn, string reason) { if (issueKeys.Add(sn + "|" + reason)) issues.Add(new WarehouseRentIssue { Sn = sn, Reason = reason }); }
            var active = new HashSet<string>();
            void unresolved(string sn, string sku, string reason, DateTime? received = null, DateTime? processed = null)
            {
                if (sn.Length == 0 || !rows.Any(r => r.Sn == sn && r.ReceivedDate == received && !r.MonthDays.HasValue))
                    rows.Add(new WarehouseRentRow { Sn = sn, Sku = sku, ReceivedDate = received, ProcessingTime = processed, Basis = "待核对：" + reason });
            }
            int future = 0;
            void add(string sn, string sku, DateTime received, DateTime until, DateTime? processed, string basis)
            {
                DateTime first = received.Date > start ? received.Date : start;
                rows.Add(new WarehouseRentRow { Sn = sn, Sku = sku, ReceivedDate = received.Date, EndDate = until.Date, ProcessingTime = processed,
                    MonthDays = (until.Date - first).Days + 1, TotalDays = (until.Date - received.Date).Days + 1, Basis = basis });
            }
            foreach (var group in inventory.GroupBy(r => Key(r.Sn)))
            {
                string sn = group.Key;
                if (sn.Length == 0) { issue("", "在仓清单 SN 为空"); unresolved("", group.First().Sku, "在仓清单 SN 为空"); active.Add(sn); continue; }
                active.Add(sn); // Inventory always takes precedence over completion records, even when receipt is unresolved.
                var item = group.First();
                if (group.Select(r => (r.Sku ?? "").Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
                { issue(sn, "在仓清单同一 SN 对应多个 SKU"); unresolved(sn, item.Sku, "在仓清单同一 SN 对应多个 SKU"); continue; }
                if (group.Any(r => r.ShelvingDate.HasValue && r.ShelvingDate.Value.Date > snapshotDate.Date))
                { issue(sn, "上架日期晚于清单日期，请检查清单日期"); unresolved(sn, item.Sku, "上架日期晚于清单日期"); continue; }
                var matches = receipts[sn].Where(r => r.ReceivedDate.Date <= snapshotDate.Date).ToList();
                if (matches.Count == 0) { issue(sn, "在仓机器未匹配到有效入仓日期"); unresolved(sn, item.Sku, "未匹配入仓日期"); continue; }
                var receipt = matches.First();
                if (receipt.ReceivedDate.Date > end) { future++; continue; }
                DateTime? processed = null;
                foreach (var record in baseRows[sn])
                {
                    DateTime time;
                    if (source.TryDate(record, out time) && time.Date >= receipt.ReceivedDate.Date && time.Date <= end && (!processed.HasValue || time > processed.Value)) processed = time;
                }
                add(sn, string.IsNullOrWhiteSpace(item.Sku) ? receipt.Model : item.Sku, receipt.ReceivedDate, end, processed, "仍在仓（清单优先）");
            }
            var settled = new Dictionary<string, WarehouseRentRow>();
            foreach (var record in source.Rows)
            {
                if (!completedStatuses.Contains(source.Status(record))) continue;
                string sn = Key(value(record, snColumn));
                if (sn.Length > 0 && active.Contains(sn)) continue;
                DateTime processed;
                if (!source.TryDate(record, out processed)) { issue(sn, "已处理记录的处理时间为空或无效，无法确认结算月份"); continue; }
                if (processed.Year != year || processed.Month != month) continue;
                if (sn.Length == 0) { issue(sn, "当月处理完成记录 SN 为空"); unresolved(sn, value(record, skuColumn), "当月处理完成记录 SN 为空", processed: processed); continue; }
                var matches = receipts[sn].Where(r => r.ReceivedDate.Date <= processed.Date).ToList();
                if (matches.Count == 0) { issue(sn, "当月处理完成机器未匹配到有效入仓日期"); unresolved(sn, value(record, skuColumn), "未匹配入仓日期", processed: processed); continue; }
                var receipt = matches.First();
                string episode = sn + "|" + receipt.ReceivedDate.Date.Ticks;
                if (settled.TryGetValue(episode, out var existing))
                {
                    if (processed >= existing.ProcessingTime.Value) continue;
                    rows.Remove(existing);
                }
                add(sn, value(record, skuColumn).Length > 0 ? value(record, skuColumn) : receipt.Model,
                    receipt.ReceivedDate, processed.Date, processed, "当月处理完成");
                settled[episode] = rows.Last();
            }
            // Include every selected-month receipt, including machines absent from both other sources.
            foreach (var receipt in monthlyReceipts)
            {
                string sn = Key(receipt.SerialNumber);
                if (sn.Length == 0)
                { issue(sn, "当月收货记录 SN 为空"); unresolved(sn, receipt.Model, "当月收货记录 SN 为空", receipt.ReceivedDate); continue; }
                if (rows.Any(r => r.Sn == sn && r.ReceivedDate == receipt.ReceivedDate.Date)) continue;
                if (rows.Any(r => r.Sn == sn && !r.MonthDays.HasValue))
                {
                    var placeholder = rows.FirstOrDefault(r => r.Sn == sn && !r.ReceivedDate.HasValue && !r.MonthDays.HasValue);
                    if (placeholder != null) placeholder.ReceivedDate = receipt.ReceivedDate.Date;
                    else unresolved(sn, receipt.Model, "来源匹配存在异常，需确认入仓批次", receipt.ReceivedDate);
                    continue;
                }
                add(sn, receipt.Model, receipt.ReceivedDate, end, null, "当月收货（截至月末）");
            }
            foreach (var row in rows)
            {
                row.ReceiptSource = row.ReceivedDate.HasValue ? row.ReceivedDate.Value.Year == year && row.ReceivedDate.Value.Month == month : monthlyReceiptSn.Contains(row.Sn);
                row.SystemSource = completedSn.Contains(row.Sn);
                row.InventorySource = active.Contains(row.Sn);
                if (row.MonthDays.HasValue)
                {
                    decimal rate;
                    var receipt = receipts[row.Sn].FirstOrDefault();
                    if (WarehouseRentRates.TryGet(row.Sku, out rate) || WarehouseRentRates.TryGet(receipt?.Model, out rate))
                        row.MonthRent = row.MonthDays.Value * rate;
                    else issue(row.Sn, "未匹配日费率：" + row.Sku);
                }
            }
            foreach (var row in issues)
            {
                row.ReceiptSource = monthlyReceiptSn.Contains(row.Sn); row.SystemSource = completedSn.Contains(row.Sn); row.InventorySource = active.Contains(row.Sn);
            }
            return new WarehouseRentResult { Rows = rows.OrderBy(r => r.Sn, StringComparer.OrdinalIgnoreCase).ToList().AsReadOnly(), Issues = issues.AsReadOnly(), FutureReceiptCount = future };
        }

        // A header-based importer accepts both the Web's database column names and a compact SN/SKU workbook.
        public static IReadOnlyList<WarehouseInventoryRow> Import(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var strings = zip.GetEntry("xl/sharedStrings.xml") == null ? new List<string>() : MonthlyReportSource.ReadXml(zip, "xl/sharedStrings.xml").Descendants(Ns + "si").Select(si => string.Concat(si.Descendants(Ns + "t").Select(t => t.Value))).ToList();
                string text(XElement c) => (string)c.Attribute("t") == "s" ? strings[int.Parse(c.Element(Ns + "v").Value)] : MonthlyReportSource.Value(c);
                XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
                var book = MonthlyReportSource.ReadXml(zip, "xl/workbook.xml");
                bool date1904 = new[] { "true", "1" }.Contains((string)book.Root.Element(Ns + "workbookPr")?.Attribute("date1904"));
                var first = book.Descendants(Ns + "sheet").First();
                var target = (string)MonthlyReportSource.ReadXml(zip, "xl/_rels/workbook.xml.rels").Root.Elements().First(r => (string)r.Attribute("Id") == (string)first.Attribute(rel + "id")).Attribute("Target");
                target = target.Replace('\\', '/').TrimStart('/');
                var data = MonthlyReportSource.ReadXml(zip, target.StartsWith("xl/") ? target : "xl/" + target).Descendants(Ns + "row").ToList();
                var header = data.Take(10).FirstOrDefault(r => r.Elements(Ns + "c").Any(c => text(c).Trim().Equals("sn", StringComparison.OrdinalIgnoreCase)));
                if (header == null) throw new InvalidDataException("在仓清单缺少 SN 表头。");
                var columns = header.Elements(Ns + "c").ToDictionary(c => text(c).Trim().ToLowerInvariant(), MonthlyReportSource.Column);
                string val(XElement row, params string[] names) { foreach (string name in names) if (columns.TryGetValue(name, out var col)) { var c = row.Elements(Ns + "c").FirstOrDefault(x => MonthlyReportSource.Column(x) == col); return c == null ? "" : text(c).Trim(); } return ""; }
                var result = new List<WarehouseInventoryRow>();
                foreach (var row in data.SkipWhile(r => r != header).Skip(1))
                {
                    string sn = val(row, "sn"), sku = val(row, "sku", "机型"), pallet = val(row, "pallet_number", "托盘号"), shelving = val(row, "shelving_date", "上架日期");
                    if (sn.Length == 0 && sku.Length == 0 && pallet.Length == 0 && shelving.Length == 0) continue;
                    DateTime? date = null;
                    if (shelving.Length > 0)
                    {
                        double serial; DateTime parsed;
                        if (double.TryParse(shelving, NumberStyles.Float, CultureInfo.InvariantCulture, out serial))
                        { try { date = DateTime.FromOADate(serial + (date1904 ? 1462 : 0)).Date; } catch (ArgumentException) { throw new InvalidDataException("上架日期无效：" + shelving); } }
                        else if (DateTime.TryParse(shelving, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)) date = parsed.Date;
                        else throw new InvalidDataException("上架日期无效：" + shelving);
                    }
                    result.Add(new WarehouseInventoryRow { Sn = sn, Sku = sku, PalletNumber = pallet, ShelvingDate = date });
                }
                return result.AsReadOnly();
            }
        }

        public static void ExportTemplate(string path) => WriteTables(path, new[] { "在仓清单" }, new[] {
            new List<object[]> { new object[] { "uuid", "number", "sn", "sku", "type", "processing_method", "processing_time", "shelving_date", "pallet_number", "shelving_time" } } });
        public static void Export(WarehouseRentResult result, string path)
        {
            var bills = new List<object[]> { new object[] { "SN", "SKU/机型", "入仓日期", "结算截止日期", "处理时间", "当月在仓天数", "累计在仓天数", "结算依据", "收货表", "系统导出", "依然在仓库", "当月仓租" } };
            bills.AddRange(result.Rows.Select(r => new object[] { r.Sn, r.Sku, r.ReceivedDate, r.EndDate, r.ProcessingTime, r.MonthDays, r.TotalDays, r.Basis, r.ReceiptSource ? "是" : "否", r.SystemSource ? "是" : "否", r.InventorySource ? "是" : "否", r.MonthRent }));
            var issues = new List<object[]> { new object[] { "SN", "待核对原因", "收货表", "系统导出", "依然在仓库" } };
            issues.AddRange(result.Issues.Select(r => new object[] { r.Sn, r.Reason, r.ReceiptSource ? "是" : "否", r.SystemSource ? "是" : "否", r.InventorySource ? "是" : "否" }));
            WriteTables(path, new[] { "仓租结算", "待核对" }, new[] { bills, issues });
        }
        internal static void WriteTables(string path, string[] names, List<object[]>[] tables)
        {
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships", pkg = "http://schemas.openxmlformats.org/package/2006/relationships", ct = "http://schemas.openxmlformats.org/package/2006/content-types";
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var zip = ZipFile.Open(temporary, ZipArchiveMode.Create))
                {
                    void write(string part, XElement root) { using (var stream = zip.CreateEntry(part).Open()) new XDocument(root).Save(stream); }
                    var types = new XElement(ct + "Types", new XElement(ct + "Default", new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")), new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")));
                    void content(string part, string type) => types.Add(new XElement(ct + "Override", new XAttribute("PartName", "/" + part), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml." + type + "+xml")));
                    var links = new XElement(pkg + "Relationships"); var sheets = new XElement(Ns + "sheets");
                    for (int s = 0; s < tables.Length; s++)
                    {
                        string id = "rId" + (s + 1), part = "worksheets/sheet" + (s + 1) + ".xml";
                        links.Add(new XElement(pkg + "Relationship", new XAttribute("Id", id), new XAttribute("Type", rel.NamespaceName + "/worksheet"), new XAttribute("Target", part)));
                        sheets.Add(new XElement(Ns + "sheet", new XAttribute("name", names[s]), new XAttribute("sheetId", s + 1), new XAttribute(rel + "id", id)));
                        content("xl/" + part, "worksheet");
                        var data = new XElement(Ns + "sheetData");
                        int r = 0;
                        foreach (var values in tables[s])
                        {
                            r++; var row = new XElement(Ns + "row", new XAttribute("r", r));
                            for (int c = 0; c < values.Length; c++)
                            {
                                var cell = new XElement(Ns + "c", new XAttribute("r", ((char)('A' + c)).ToString() + r));
                                var value = values[c];
                                if (value is DateTime) { cell.SetAttributeValue("s", s == 0 && c == 4 ? 3 : 1); cell.Add(new XElement(Ns + "v", ((DateTime)value).ToOADate().ToString(CultureInfo.InvariantCulture))); }
                                else if (value is int) cell.Add(new XElement(Ns + "v", value));
                                else if (value is decimal) { cell.SetAttributeValue("s", 4); cell.Add(new XElement(Ns + "v", ((decimal)value).ToString(CultureInfo.InvariantCulture))); }
                                else { cell.SetAttributeValue("t", "inlineStr"); cell.Add(new XElement(Ns + "is", new XElement(Ns + "t", value?.ToString() ?? ""))); }
                                row.Add(cell);
                            }
                            data.Add(row);
                        }
                        int count = tables[s][0].Length;
                        write("xl/" + part, new XElement(Ns + "worksheet", new XElement(Ns + "sheetViews", new XElement(Ns + "sheetView", new XAttribute("workbookViewId", 0), new XElement(Ns + "pane", new XAttribute("ySplit", 1), new XAttribute("topLeftCell", "A2"), new XAttribute("state", "frozen")))), new XElement(Ns + "cols", new XElement(Ns + "col", new XAttribute("min", 1), new XAttribute("max", count), new XAttribute("width", s == 1 ? 55 : 28), new XAttribute("customWidth", 1))), data, new XElement(Ns + "autoFilter", new XAttribute("ref", "A1:" + (char)('A' + count - 1) + r))));
                    }
                    content("xl/workbook.xml", "sheet.main"); content("xl/styles.xml", "styles");
                    links.Add(new XElement(pkg + "Relationship", new XAttribute("Id", "styles"), new XAttribute("Type", rel.NamespaceName + "/styles"), new XAttribute("Target", "styles.xml")));
                    write("xl/styles.xml", RegistrationRepairService.Styles());
                    write("xl/workbook.xml", new XElement(Ns + "workbook", sheets)); write("xl/_rels/workbook.xml.rels", links);
                    write("_rels/.rels", new XElement(pkg + "Relationships", new XElement(pkg + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Type", rel.NamespaceName + "/officeDocument"), new XAttribute("Target", "xl/workbook.xml"))));
                    write("[Content_Types].xml", types);
                }
                if (File.Exists(path)) File.Replace(temporary, path, null); else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}

