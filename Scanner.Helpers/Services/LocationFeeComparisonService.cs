using Scanner.Models;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace Scanner.Helpers.Services
{
    public sealed class LocationFeeComparisonSummary
    {
        public int SheetCount { get; set; }
        public int RowCount { get; set; }
        public int MatchedCount { get; set; }
        public int UnmatchedCount { get; set; }
    }

    public static class LocationFeeComparisonService
    {
        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace OfficeRelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";
        private const string DrawingRelationshipType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/drawing";

        public static LocationFeeComparisonSummary Build(string templatePath, string baseWorkbookPath, string outputPath)
        {
            return Build(templatePath, ChecklistXlsxReader.Read(baseWorkbookPath), outputPath);
        }

        public static LocationFeeComparisonSummary Build(string templatePath, IEnumerable<InboundChecklistRecord> baseRecords, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(templatePath) || !File.Exists(templatePath)) throw new FileNotFoundException("找不到库位付费模板。", templatePath);
            if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("输出路径不能为空。", nameof(outputPath));
            if (string.Equals(Path.GetFullPath(templatePath), Path.GetFullPath(outputPath), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("输出文件不能覆盖原模板，请选择其他文件名。");

            IDictionary<string, InboundChecklistRecord> baseBySn = baseRecords
                .Where(item => item != null && !string.IsNullOrWhiteSpace(item.Sn))
                .GroupBy(item => NormalizeSn(item.Sn), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);

            string outputDirectory = Path.GetDirectoryName(Path.GetFullPath(outputPath));
            if (!Directory.Exists(outputDirectory)) Directory.CreateDirectory(outputDirectory);
            string temporaryPath = outputPath + ".tmp";
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);

            var summary = new LocationFeeComparisonSummary();
            try
            {
                using (FileStream sourceStream = new FileStream(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (ZipArchive source = new ZipArchive(sourceStream, ZipArchiveMode.Read, false))
                {
                    IList<string> sharedStrings = ReadSharedStrings(source);
                    ISet<string> worksheetPaths = ReadWorksheetPaths(source);
                    if (worksheetPaths.Count != 2) throw new InvalidDataException(string.Format("模板必须包含两个工作表，当前检测到 {0} 个。", worksheetPaths.Count));

                    using (ZipArchive destination = ZipFile.Open(temporaryPath, ZipArchiveMode.Create))
                    {
                        foreach (ZipArchiveEntry entry in source.Entries)
                        {
                            string entryPath = entry.FullName.Replace('\\', '/');
                            if (entryPath.StartsWith("xl/drawings/", StringComparison.OrdinalIgnoreCase) ||
                                entryPath.StartsWith("xl/media/", StringComparison.OrdinalIgnoreCase)) continue;

                            if (worksheetPaths.Contains(entryPath))
                            {
                                XDocument sheet;
                                using (Stream stream = entry.Open()) sheet = XDocument.Load(stream);
                                ProcessWorksheet(sheet, sharedStrings, baseBySn, summary);
                                WriteDocument(destination, entryPath, sheet);
                            }
                            else if (IsWorksheetRelationship(entryPath, worksheetPaths))
                            {
                                XDocument relationships;
                                using (Stream stream = entry.Open()) relationships = XDocument.Load(stream);
                                relationships.Descendants(PackageRelationshipNs + "Relationship")
                                    .Where(item => string.Equals((string)item.Attribute("Type"), DrawingRelationshipType, StringComparison.OrdinalIgnoreCase))
                                    .Remove();
                                WriteDocument(destination, entryPath, relationships);
                            }
                            else if (string.Equals(entryPath, "[Content_Types].xml", StringComparison.OrdinalIgnoreCase))
                            {
                                XDocument contentTypes;
                                using (Stream stream = entry.Open()) contentTypes = XDocument.Load(stream);
                                contentTypes.Root.Elements().Where(item =>
                                {
                                    string partName = ((string)item.Attribute("PartName") ?? string.Empty).TrimStart('/');
                                    return partName.StartsWith("xl/drawings/", StringComparison.OrdinalIgnoreCase) || partName.StartsWith("xl/media/", StringComparison.OrdinalIgnoreCase);
                                }).Remove();
                                WriteDocument(destination, entryPath, contentTypes);
                            }
                            else CopyEntry(entry, destination);
                        }
                    }
                }

                if (File.Exists(outputPath)) File.Delete(outputPath);
                File.Move(temporaryPath, outputPath);
                return summary;
            }
            catch
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
                throw;
            }
        }

        private static void ProcessWorksheet(XDocument sheet, IList<string> sharedStrings, IDictionary<string, InboundChecklistRecord> baseBySn, LocationFeeComparisonSummary summary)
        {
            sheet.Descendants(MainNs + "drawing").Remove();
            XElement sheetData = sheet.Descendants(MainNs + "sheetData").FirstOrDefault();
            if (sheetData == null) throw new InvalidDataException("工作表缺少 sheetData。 ");

            DateTime? currentDate = null;
            string currentDateStyle = null;
            int maximumRow = 0;
            foreach (XElement row in sheetData.Elements(MainNs + "row"))
            {
                int rowNumber;
                if (!int.TryParse((string)row.Attribute("r"), out rowNumber) || rowNumber <= 0) continue;
                maximumRow = Math.Max(maximumRow, rowNumber);

                XElement dateCell = FindCell(row, "A" + rowNumber);
                DateTime parsedDate;
                if (TryReadDate(dateCell, sharedStrings, out parsedDate))
                {
                    currentDate = parsedDate.Date;
                    currentDateStyle = (string)dateCell.Attribute("s") ?? currentDateStyle;
                }
                if (currentDate.HasValue) SetNumberCell(row, "A" + rowNumber, currentDate.Value.ToOADate(), currentDateStyle);

                string sn = ReadCellText(FindCell(row, "C" + rowNumber), sharedStrings);
                if (string.IsNullOrWhiteSpace(sn))
                {
                    SetTextCell(row, "I" + rowNumber, string.Empty);
                    SetTextCell(row, "J" + rowNumber, string.Empty);
                    continue;
                }

                summary.RowCount++;
                InboundChecklistRecord matched;
                if (baseBySn.TryGetValue(NormalizeSn(sn), out matched))
                {
                    summary.MatchedCount++;
                    SetTextCell(row, "I" + rowNumber, matched.ProcessingTime);
                    SetTextCell(row, "J" + rowNumber, matched.DetectionStatus);
                }
                else
                {
                    summary.UnmatchedCount++;
                    SetTextCell(row, "I" + rowNumber, string.Empty);
                    SetTextCell(row, "J" + rowNumber, "不存在");
                }
            }
            summary.SheetCount++;
            ExtendDimension(sheet, maximumRow);
            EnsureOutputColumns(sheet);
        }

        private static bool TryReadDate(XElement cell, IList<string> sharedStrings, out DateTime date)
        {
            date = default(DateTime);
            if (cell == null) return false;
            string type = (string)cell.Attribute("t");
            string raw = ReadCellText(cell, sharedStrings);
            double serial;
            if (type != "s" && type != "inlineStr" && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out serial) && serial >= 20000 && serial <= 100000)
            {
                try { date = DateTime.FromOADate(serial).Date; return true; }
                catch (ArgumentException) { return false; }
            }
            return DateTime.TryParse(raw, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out date) ||
                   DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out date);
        }

        private static void ExtendDimension(XDocument sheet, int maximumRow)
        {
            if (maximumRow <= 0) return;
            XElement dimension = sheet.Root.Element(MainNs + "dimension");
            if (dimension == null)
            {
                dimension = new XElement(MainNs + "dimension");
                sheet.Root.AddFirst(dimension);
            }
            dimension.SetAttributeValue("ref", "A1:J" + maximumRow);
        }

        private static void EnsureOutputColumns(XDocument sheet)
        {
            XElement cols = sheet.Root.Element(MainNs + "cols");
            if (cols == null)
            {
                XElement sheetData = sheet.Root.Element(MainNs + "sheetData");
                cols = new XElement(MainNs + "cols");
                if (sheetData != null) sheetData.AddBeforeSelf(cols); else sheet.Root.Add(cols);
            }
            if (!ColumnHasDefinition(cols, 9)) cols.Add(new XElement(MainNs + "col", new XAttribute("min", "9"), new XAttribute("max", "9"), new XAttribute("width", "20"), new XAttribute("customWidth", "1")));
            if (!ColumnHasDefinition(cols, 10)) cols.Add(new XElement(MainNs + "col", new XAttribute("min", "10"), new XAttribute("max", "10"), new XAttribute("width", "16"), new XAttribute("customWidth", "1")));
        }

        private static bool ColumnHasDefinition(XElement cols, int columnIndex)
        {
            return cols.Elements(MainNs + "col").Any(column =>
            {
                int min;
                int max;
                return int.TryParse((string)column.Attribute("min"), out min) && int.TryParse((string)column.Attribute("max"), out max) && min <= columnIndex && max >= columnIndex;
            });
        }

        private static void SetTextCell(XElement row, string reference, string value)
        {
            XElement cell = ReplaceCell(row, reference);
            cell.SetAttributeValue("t", "inlineStr");
            cell.Add(new XElement(MainNs + "is", new XElement(MainNs + "t", value ?? string.Empty)));
        }

        private static void SetNumberCell(XElement row, string reference, double value, string style)
        {
            XElement cell = ReplaceCell(row, reference);
            if (!string.IsNullOrWhiteSpace(style)) cell.SetAttributeValue("s", style);
            cell.Add(new XElement(MainNs + "v", value.ToString("0.##########", CultureInfo.InvariantCulture)));
        }

        private static XElement ReplaceCell(XElement row, string reference)
        {
            XElement existing = FindCell(row, reference);
            if (existing != null) existing.Remove();
            var cell = new XElement(MainNs + "c", new XAttribute("r", reference));
            int targetColumn = GetColumnIndex(reference);
            XElement next = row.Elements(MainNs + "c").FirstOrDefault(item => GetColumnIndex((string)item.Attribute("r")) > targetColumn);
            if (next == null) row.Add(cell); else next.AddBeforeSelf(cell);
            return cell;
        }

        private static XElement FindCell(XElement row, string reference)
        {
            return row.Elements(MainNs + "c").FirstOrDefault(item => string.Equals((string)item.Attribute("r"), reference, StringComparison.OrdinalIgnoreCase));
        }

        private static string ReadCellText(XElement cell, IList<string> sharedStrings)
        {
            if (cell == null) return string.Empty;
            string type = (string)cell.Attribute("t");
            if (type == "inlineStr") return string.Concat(cell.Descendants(MainNs + "t").Select(item => item.Value));
            string value = (string)cell.Element(MainNs + "v") ?? string.Empty;
            int index;
            if (type == "s" && int.TryParse(value, out index) && index >= 0 && index < sharedStrings.Count) return sharedStrings[index];
            return value;
        }

        private static IList<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return new List<string>();
            XDocument document;
            using (Stream stream = entry.Open()) document = XDocument.Load(stream);
            return document.Descendants(MainNs + "si").Select(item => string.Concat(item.Descendants(MainNs + "t").Select(text => text.Value))).ToList();
        }

        private static ISet<string> ReadWorksheetPaths(ZipArchive archive)
        {
            XDocument workbook;
            XDocument relationships;
            using (Stream stream = RequiredEntry(archive, "xl/workbook.xml").Open()) workbook = XDocument.Load(stream);
            using (Stream stream = RequiredEntry(archive, "xl/_rels/workbook.xml.rels").Open()) relationships = XDocument.Load(stream);
            var targets = relationships.Descendants(PackageRelationshipNs + "Relationship")
                .ToDictionary(item => (string)item.Attribute("Id"), item => (string)item.Attribute("Target"), StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (XElement sheet in workbook.Descendants(MainNs + "sheet"))
            {
                string target;
                string id = (string)sheet.Attribute(OfficeRelationshipNs + "id");
                if (string.IsNullOrWhiteSpace(id) || !targets.TryGetValue(id, out target)) continue;
                paths.Add(NormalizePartPath("xl", target));
            }
            return paths;
        }

        private static bool IsWorksheetRelationship(string entryPath, ISet<string> worksheetPaths)
        {
            foreach (string worksheetPath in worksheetPaths)
            {
                string directory = Path.GetDirectoryName(worksheetPath).Replace('\\', '/');
                string relationshipPath = directory + "/_rels/" + Path.GetFileName(worksheetPath) + ".rels";
                if (string.Equals(entryPath, relationshipPath, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }

        private static string NormalizePartPath(string baseDirectory, string target)
        {
            string combined = (baseDirectory.TrimEnd('/') + "/" + (target ?? string.Empty).Replace('\\', '/').TrimStart('/'));
            var segments = new List<string>();
            foreach (string segment in combined.Split('/'))
            {
                if (segment == "." || segment.Length == 0) continue;
                if (segment == "..") { if (segments.Count > 0) segments.RemoveAt(segments.Count - 1); }
                else segments.Add(segment);
            }
            return string.Join("/", segments);
        }

        private static string NormalizeSn(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return string.Empty;
            var builder = new StringBuilder(value.Length);
            foreach (char character in value)
            {
                if (!char.IsWhiteSpace(character) && character != '\uFEFF' && character != '\u200B') builder.Append(character);
            }
            return builder.ToString();
        }

        private static int GetColumnIndex(string reference)
        {
            int result = 0;
            foreach (char character in reference ?? string.Empty)
            {
                if (!char.IsLetter(character)) break;
                result = result * 26 + char.ToUpperInvariant(character) - 'A' + 1;
            }
            return result;
        }

        private static ZipArchiveEntry RequiredEntry(ZipArchive archive, string path)
        {
            ZipArchiveEntry entry = archive.GetEntry(path);
            if (entry == null) throw new InvalidDataException("Excel 文件结构不完整：" + path);
            return entry;
        }

        private static void CopyEntry(ZipArchiveEntry source, ZipArchive destination)
        {
            ZipArchiveEntry target = destination.CreateEntry(source.FullName, CompressionLevel.Optimal);
            using (Stream input = source.Open()) using (Stream output = target.Open()) input.CopyTo(output);
        }

        private static void WriteDocument(ZipArchive archive, string path, XDocument document)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using (Stream stream = entry.Open()) using (var writer = new StreamWriter(stream, new UTF8Encoding(false))) document.Save(writer);
        }
    }
}
