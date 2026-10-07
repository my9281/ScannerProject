using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

namespace Scanner.Helpers.Services
{
    public sealed class BatchLabelRecord
    {
        public string SheetName { get; internal set; }
        public int RowNumber { get; internal set; }
        public string Sku { get; internal set; }
        public string Sn { get; internal set; }
        public string Treatment { get; internal set; }
        public string Remark { get; internal set; }
        public int Quantity { get; internal set; }
        public bool IsScrapped => Treatment == "报废" || Treatment == "报废处理";
        public string LabelRemark => IsScrapped ? "报废 / Scrapped / Desechado" : Remark;
        public string Error { get; internal set; }
        public int SentCount { get; set; }
        public string PrintStatus { get; set; } = "待打印";
    }

    public static class BatchLabelImportService
    {
        public static IReadOnlyList<BatchLabelRecord> Load(string path)
        {
            XNamespace ns = MonthlyReportSource.Ns, rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var strings = zip.GetEntry("xl/sharedStrings.xml") == null ? new List<string>() : MonthlyReportSource.ReadXml(zip, "xl/sharedStrings.xml").Descendants(ns + "si").Select(s => string.Concat(s.Descendants(ns + "t").Select(t => t.Value))).ToList();
                string value(XElement c) => c == null ? "" : (string)c.Attribute("t") == "s" ? strings[int.Parse(c.Element(ns + "v").Value, CultureInfo.InvariantCulture)] : MonthlyReportSource.Value(c);
                var book = MonthlyReportSource.ReadXml(zip, "xl/workbook.xml");
                var links = MonthlyReportSource.ReadXml(zip, "xl/_rels/workbook.xml.rels").Root.Elements().ToDictionary(r => (string)r.Attribute("Id"), r => (string)r.Attribute("Target"));
                var result = new List<BatchLabelRecord>();
                int found = 0;
                foreach (var sheet in book.Descendants(ns + "sheet"))
                {
                    string target = links[(string)sheet.Attribute(rel + "id")].Replace('\\', '/').TrimStart('/');
                    var rows = MonthlyReportSource.ReadXml(zip, target.StartsWith("xl/") ? target : "xl/" + target).Descendants(ns + "sheetData").Elements(ns + "row").ToList();
                    var header = rows.Take(10).FirstOrDefault(r => r.Elements(ns + "c").Any(c => value(c).Trim().Equals("SKU", StringComparison.OrdinalIgnoreCase)) && r.Elements(ns + "c").Any(c => value(c).Trim().Equals("SN", StringComparison.OrdinalIgnoreCase)));
                    if (header == null) continue;
                    found++;
                    var columns = header.Elements(ns + "c").Where(c => value(c).Trim().Length > 0).ToDictionary(c => value(c).Trim(), MonthlyReportSource.Column, StringComparer.OrdinalIgnoreCase);
                    if (!columns.ContainsKey("处理") || !columns.ContainsKey("备注")) throw new InvalidDataException((string)sheet.Attribute("name") + " 缺少“处理”或“备注”列。");
                    foreach (var row in rows.SkipWhile(r => r != header).Skip(1))
                    {
                        string text(string name) => columns.TryGetValue(name, out var col) ? value(row.Elements(ns + "c").FirstOrDefault(c => MonthlyReportSource.Column(c) == col)).Trim() : "";
                        string sku = text("SKU"), sn = text("SN"), treatment = text("处理"), remark = text("备注"), quantityText = text("数量");
                        if (sku.Length == 0 && sn.Length == 0 && row.Elements(ns + "c").Any(c => new[] { "合计", "总计", "TOTAL" }.Contains(value(c).Trim().ToUpperInvariant()))) continue;
                        if (new[] { sku, sn, treatment, remark, quantityText }.All(string.IsNullOrWhiteSpace)) continue;
                        var item = new BatchLabelRecord { SheetName = (string)sheet.Attribute("name"), RowNumber = (int)row.Attribute("r"), Sku = sku, Sn = sn, Treatment = treatment, Remark = remark, Quantity = 1 };
                        if (sku.Length == 0 || sn.Length == 0) item.Error = "SKU 或 SN 为空";
                        if (quantityText.Length > 0)
                        {
                            decimal quantity;
                            if (!decimal.TryParse(quantityText, NumberStyles.Number, CultureInfo.InvariantCulture, out quantity) || quantity <= 0 || quantity != decimal.Truncate(quantity) || quantity > int.MaxValue)
                                item.Error = "数量必须是正整数";
                            else item.Quantity = (int)quantity;
                        }
                        if (item.Error != null) item.PrintStatus = "跳过：" + item.Error;
                        result.Add(item);
                    }
                }
                if (found == 0) throw new InvalidDataException("未找到包含 SKU、SN、处理、备注列的工作表。");
                return result.AsReadOnly();
            }
        }
    }
}
