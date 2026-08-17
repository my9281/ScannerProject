using Scanner.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Scanner.Services
{
    public static class ChecklistXlsxReader
    {
        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelationshipNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelationshipNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static IList<InboundChecklistRecord> Read(string path)
        {
            using (ZipArchive archive = ZipFile.OpenRead(path))
            {
                IList<string> sharedStrings = ReadSharedStrings(archive);
                XDocument sheet = LoadFirstSheet(archive);
                List<InboundChecklistRecord> records = new List<InboundChecklistRecord>();
                foreach (XElement row in sheet.Descendants(MainNs + "row"))
                {
                    int rowNumber;
                    int.TryParse((string)row.Attribute("r"), out rowNumber);
                    if (rowNumber <= 2) continue;
                    string[] values = new string[22];
                    foreach (XElement cell in row.Elements(MainNs + "c"))
                    {
                        int column = GetColumnIndex((string)cell.Attribute("r"));
                        if (column >= 0 && column < values.Length) values[column] = GetCellValue(cell, sharedStrings, column);
                    }
                    if (string.IsNullOrWhiteSpace(values[9])) continue;
                    records.Add(new InboundChecklistRecord
                    {
                        Type = values[1], Sn = values[9], DetectionStatus = values[12], ProcessingTime = values[14]
                    });
                }
                return records;
            }
        }

        private static XDocument LoadFirstSheet(ZipArchive archive)
        {
            XDocument workbook;
            using (Stream stream = RequiredEntry(archive, "xl/workbook.xml").Open()) workbook = XDocument.Load(stream);
            XElement firstSheet = workbook.Descendants(MainNs + "sheet").FirstOrDefault();
            if (firstSheet == null) throw new InvalidDataException("工作簿中没有工作表。");
            string relationshipId = (string)firstSheet.Attribute(RelationshipNs + "id");
            XDocument relationships;
            using (Stream stream = RequiredEntry(archive, "xl/_rels/workbook.xml.rels").Open()) relationships = XDocument.Load(stream);
            XElement relationship = relationships.Descendants(PackageRelationshipNs + "Relationship").FirstOrDefault(x => (string)x.Attribute("Id") == relationshipId);
            if (relationship == null) throw new InvalidDataException("无法定位第一个工作表。");
            string target = ((string)relationship.Attribute("Target") ?? string.Empty).Replace('\\', '/').TrimStart('/');
            string entryPath = target.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? target : "xl/" + target;
            using (Stream stream = RequiredEntry(archive, entryPath).Open()) return XDocument.Load(stream);
        }

        private static IList<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return new List<string>();
            XDocument document;
            using (Stream stream = entry.Open()) document = XDocument.Load(stream);
            return document.Descendants(MainNs + "si").Select(x => string.Concat(x.Descendants(MainNs + "t").Select(t => t.Value))).ToList();
        }

        private static string GetCellValue(XElement cell, IList<string> sharedStrings, int column)
        {
            string type = (string)cell.Attribute("t");
            if (type == "inlineStr") return string.Concat(cell.Descendants(MainNs + "t").Select(x => x.Value));
            string value = (string)cell.Element(MainNs + "v") ?? string.Empty;
            int index;
            if (type == "s" && int.TryParse(value, out index) && index >= 0 && index < sharedStrings.Count) return sharedStrings[index];
            double serialDate;
            if (column == 14 && double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out serialDate) && serialDate > 0)
            {
                try { return DateTime.FromOADate(serialDate).ToString("yyyy-MM-dd HH:mm:ss"); } catch (ArgumentException) { }
            }
            return value;
        }

        private static int GetColumnIndex(string reference)
        {
            int result = 0;
            foreach (char c in reference ?? string.Empty)
            {
                if (!char.IsLetter(c)) break;
                result = result * 26 + char.ToUpperInvariant(c) - 'A' + 1;
            }
            return result - 1;
        }

        private static ZipArchiveEntry RequiredEntry(ZipArchive archive, string path)
        {
            ZipArchiveEntry entry = archive.GetEntry(path);
            if (entry == null) throw new InvalidDataException("Excel 文件结构不完整：" + path);
            return entry;
        }
    }
}
