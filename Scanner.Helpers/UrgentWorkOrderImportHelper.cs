using Microsoft.VisualBasic.FileIO;
using Scanner.Models;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Scanner.Helpers
{
    public sealed class UrgentWorkOrderImportHelper
    {
        private const int SnColumn = 7;
        private const int RemarkColumn = 10;
        private const int ServiceTypeColumn = 11;
        private const int TrackingColumn = 14;
        private const int WorkOrderStatusColumn = 18;
        private const int RequiredColumnCount = 19;
        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        public UrgentWorkOrderImportResult Import(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("紧急工单文件路径不能为空。", nameof(filePath));
            }
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("找不到紧急工单文件。", filePath);
            }
            string extension = Path.GetExtension(filePath);
            IReadOnlyList<string[]> rows;
            if (string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                rows = ReadXlsxRows(filePath);
            }
            else if (string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase))
            {
                rows = ReadCsvRows(filePath);
            }
            else
            {
                throw new InvalidOperationException("仅支持 .xlsx 或 .csv 紧急工单文件。");
            }
            return BuildRules(rows, Path.GetFileNameWithoutExtension(filePath));
        }

        private static UrgentWorkOrderImportResult BuildRules(IReadOnlyList<string[]> rows, string sourceName)
        {
            if (rows == null || rows.Count == 0)
            {
                throw new InvalidOperationException("紧急工单文件中没有可读取的数据。");
            }
            int headerIndex = FindHeaderIndex(rows);
            if (headerIndex < 0)
            {
                throw new InvalidOperationException("未找到表头，请确认 H、K、L、O、S 列分别为 SN码、故障描述、售后处理类型、退货运单号、工单状态。");
            }
            var snRules = new List<WorkOrderRemark>();
            var oidRules = new Dictionary<string, OidRuleBuilder>(StringComparer.OrdinalIgnoreCase);
            int importedRows = 0;
            long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            for (int index = headerIndex + 1; index < rows.Count; index++)
            {
                string[] row = rows[index];
                if (IsEmpty(row))
                {
                    continue;
                }
                string tracking = NormalizeIdentifier(GetField(row, TrackingColumn));
                string workOrderStatus = NormalizeText(GetField(row, WorkOrderStatusColumn));
                if (tracking.Length <= 4 || !string.Equals(workOrderStatus, "待收件", StringComparison.Ordinal))
                {
                    continue;
                }
                string sn = NormalizeIdentifier(GetField(row, SnColumn));
                importedRows++;
                int rowNumber = index + 1;
                string remark = NormalizeText(GetField(row, RemarkColumn));
                string serviceType = NormalizeText(GetField(row, ServiceTypeColumn));
                bool isRepair = serviceType.IndexOf("维修", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!string.IsNullOrEmpty(sn))
                {
                    snRules.Add(new WorkOrderRemark
                    {
                        Id = $"IMPORT-SN-{sourceName}-{rowNumber}",
                        Sn = sn,
                        Remark = remark,
                        IsUrgent = true,
                        IsRepair = isRepair,
                        RemarkTimestamp = timestamp
                    });
                }
                if (!string.IsNullOrEmpty(tracking))
                {
                    OidRuleBuilder builder;
                    if (!oidRules.TryGetValue(tracking, out builder))
                    {
                        builder = new OidRuleBuilder(tracking, sourceName, rowNumber, timestamp);
                        oidRules.Add(tracking, builder);
                    }
                    builder.Add(remark, isRepair);
                }
            }
            var rules = new List<WorkOrderRemark>(snRules.Count + oidRules.Count);
            rules.AddRange(snRules);
            rules.AddRange(oidRules.Values.Select(item => item.Build()));
            return new UrgentWorkOrderImportResult(rules, importedRows, snRules.Count, oidRules.Count);
        }

        private static int FindHeaderIndex(IReadOnlyList<string[]> rows)
        {
            int limit = Math.Min(rows.Count, 20);
            for (int index = 0; index < limit; index++)
            {
                string[] row = rows[index];
                if (row == null || row.Length < RequiredColumnCount)
                {
                    continue;
                }
                if (GetField(row, SnColumn).IndexOf("SN", StringComparison.OrdinalIgnoreCase) >= 0 && GetField(row, RemarkColumn).Contains("故障描述") && GetField(row, ServiceTypeColumn).Contains("售后处理类型") && GetField(row, TrackingColumn).Contains("运单号") && GetField(row, WorkOrderStatusColumn).Contains("工单状态"))
                {
                    return index;
                }
            }
            return -1;
        }

        private static IReadOnlyList<string[]> ReadCsvRows(string filePath)
        {
            var rows = new List<string[]>();
            int rowNumber = 0;
            using (var parser = new TextFieldParser(filePath, Encoding.UTF8))
            {
                parser.TextFieldType = FieldType.Delimited;
                parser.SetDelimiters(",");
                parser.HasFieldsEnclosedInQuotes = true;
                parser.TrimWhiteSpace = false;
                while (!parser.EndOfData)
                {
                    rowNumber++;
                    try
                    {
                        rows.Add(parser.ReadFields() ?? Array.Empty<string>());
                    }
                    catch (MalformedLineException ex)
                    {
                        throw new InvalidOperationException($"CSV 第 {rowNumber} 行格式错误：{ex.Message}", ex);
                    }
                }
            }
            return rows;
        }

        private static IReadOnlyList<string[]> ReadXlsxRows(string filePath)
        {
            using (FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read, false))
            {
                XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                List<string> sharedStrings = ReadSharedStrings(archive, spreadsheet);
                ZipArchiveEntry worksheetEntry = archive.GetEntry(GetFirstWorksheetPath(archive));
                if (worksheetEntry == null)
                {
                    throw new InvalidOperationException("Excel 文件中找不到第一个工作表。");
                }
                XDocument worksheet = LoadXml(worksheetEntry);
                var rows = new List<string[]>();
                foreach (XElement rowElement in worksheet.Descendants(spreadsheet + "row"))
                {
                    var values = new string[RequiredColumnCount];
                    foreach (XElement cell in rowElement.Elements(spreadsheet + "c"))
                    {
                        int columnIndex = GetColumnIndex((string)cell.Attribute("r"));
                        if (columnIndex >= 0 && columnIndex < RequiredColumnCount)
                        {
                            values[columnIndex] = ReadCellValue(cell, spreadsheet, sharedStrings);
                        }
                    }
                    rows.Add(values);
                }
                return rows;
            }
        }

        private static string GetFirstWorksheetPath(ZipArchive archive)
        {
            ZipArchiveEntry workbookEntry = archive.GetEntry("xl/workbook.xml");
            ZipArchiveEntry relationshipsEntry = archive.GetEntry("xl/_rels/workbook.xml.rels");
            if (workbookEntry == null || relationshipsEntry == null)
            {
                throw new InvalidOperationException("Excel 文件结构不完整。");
            }
            XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            XDocument workbook = LoadXml(workbookEntry);
            XElement firstSheet = workbook.Descendants(spreadsheet + "sheet").FirstOrDefault();
            string relationshipId = firstSheet == null ? null : (string)firstSheet.Attribute(relationships + "id");
            XDocument relationDocument = LoadXml(relationshipsEntry);
            XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
            XElement relation = relationDocument.Descendants(packageRelationships + "Relationship").FirstOrDefault(item => string.Equals((string)item.Attribute("Id"), relationshipId, StringComparison.Ordinal));
            string target = relation == null ? null : (string)relation.Attribute("Target");
            if (string.IsNullOrWhiteSpace(target))
            {
                throw new InvalidOperationException("Excel 文件中找不到第一个工作表关系。");
            }
            return target.StartsWith("/", StringComparison.Ordinal) ? target.TrimStart('/') : "xl/" + target.TrimStart('/');
        }

        private static List<string> ReadSharedStrings(ZipArchive archive, XNamespace spreadsheet)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }
            XDocument document = LoadXml(entry);
            return document.Descendants(spreadsheet + "si").Select(item => string.Concat(item.Descendants(spreadsheet + "t").Select(text => text.Value))).ToList();
        }

        private static XDocument LoadXml(ZipArchiveEntry entry)
        {
            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream, LoadOptions.None);
            }
        }

        private static string ReadCellValue(XElement cell, XNamespace spreadsheet, IReadOnlyList<string> sharedStrings)
        {
            string type = (string)cell.Attribute("t");
            if (string.Equals(type, "inlineStr", StringComparison.Ordinal))
            {
                return string.Concat(cell.Descendants(spreadsheet + "t").Select(text => text.Value));
            }
            string value = (string)cell.Element(spreadsheet + "v") ?? string.Empty;
            int sharedIndex;
            if (string.Equals(type, "s", StringComparison.Ordinal) && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
            {
                return sharedStrings[sharedIndex];
            }
            return value;
        }

        private static int GetColumnIndex(string cellReference)
        {
            if (string.IsNullOrWhiteSpace(cellReference))
            {
                return -1;
            }
            int index = 0;
            int position = 0;
            while (position < cellReference.Length && char.IsLetter(cellReference[position]))
            {
                index = index * 26 + char.ToUpperInvariant(cellReference[position]) - 'A' + 1;
                position++;
            }
            return index - 1;
        }

        private static bool IsEmpty(string[] fields)
        {
            return fields == null || fields.All(string.IsNullOrWhiteSpace);
        }

        private static string GetField(string[] fields, int index)
        {
            return fields == null || index < 0 || index >= fields.Length || fields[index] == null ? string.Empty : fields[index].Trim().TrimStart('\uFEFF');
        }

        internal static string NormalizeIdentifier(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }
            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                if (!char.IsWhiteSpace(character) && character != '\uFEFF' && character != '\u200B')
                {
                    builder.Append(character);
                }
            }
            return builder.ToString();
        }

        private static string NormalizeText(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? string.Empty : Whitespace.Replace(value.Trim().TrimStart('\uFEFF'), " ");
        }

        private sealed class OidRuleBuilder
        {
            private readonly List<string> _remarks = new List<string>();
            private readonly HashSet<string> _knownRemarks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            private readonly string _id;
            private readonly long _timestamp;
            private bool _isRepair;

            public OidRuleBuilder(string trackingNumber, string sourceName, int rowNumber, long timestamp)
            {
                TrackingNumber = trackingNumber;
                _id = $"IMPORT-OID-{sourceName}-{rowNumber}";
                _timestamp = timestamp;
            }

            public string TrackingNumber { get; }

            public void Add(string remark, bool isRepair)
            {
                if (!string.IsNullOrWhiteSpace(remark) && _knownRemarks.Add(remark))
                {
                    _remarks.Add(remark);
                }
                _isRepair |= isRepair;
            }

            public WorkOrderRemark Build()
            {
                return new WorkOrderRemark
                {
                    Id = _id,
                    TrackingNumber = TrackingNumber,
                    Remark = string.Join("；", _remarks),
                    IsUrgent = true,
                    IsRepair = _isRepair,
                    IsOidRule = true,
                    RemarkTimestamp = _timestamp
                };
            }
        }
    }

    public sealed class UrgentWorkOrderImportResult
    {
        public UrgentWorkOrderImportResult(IReadOnlyList<WorkOrderRemark> rules, int importedRowCount, int snRuleCount, int oidRuleCount)
        {
            Rules = rules ?? throw new ArgumentNullException(nameof(rules));
            ImportedRowCount = importedRowCount;
            SnRuleCount = snRuleCount;
            OidRuleCount = oidRuleCount;
        }

        public IReadOnlyList<WorkOrderRemark> Rules { get; }
        public int ImportedRowCount { get; }
        public int SnRuleCount { get; }
        public int OidRuleCount { get; }
    }
}
