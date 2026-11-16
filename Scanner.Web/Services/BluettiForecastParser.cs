using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Scanner.Web.Services;

public sealed record BluettiForecast(string Key, string Rma, string Carrier, string Number, string Issue, string SourceJson);

public static class BluettiForecastParser
{
    public static IReadOnlyList<BluettiForecast> Parse(Stream input)
    {
        using var zip = new ZipArchive(input, ZipArchiveMode.Read, leaveOpen: true);
        if (zip.Entries.Sum(e => e.Length) > 200_000_000) throw new InvalidDataException("预报文件解压后过大。");
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XDocument Xml(string path)
        {
            using var stream = (zip.GetEntry(path) ?? throw new InvalidDataException("预报工作簿结构不完整。")).Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, MaxCharactersInDocument = 100_000_000, XmlResolver = null });
            return XDocument.Load(reader);
        }
        var strings = zip.GetEntry("xl/sharedStrings.xml") == null ? [] : Xml("xl/sharedStrings.xml").Descendants(ns + "si")
            .Select(s => string.Concat(s.Descendants(ns + "t").Select(t => t.Value))).ToArray();
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace package = "http://schemas.openxmlformats.org/package/2006/relationships";
        string? sheetId = (string?)Xml("xl/workbook.xml").Descendants(ns + "sheet").FirstOrDefault()?.Attribute(rel + "id");
        var relationship = Xml("xl/_rels/workbook.xml.rels").Descendants(package + "Relationship").FirstOrDefault(e => (string?)e.Attribute("Id") == sheetId);
        string target = (string?)relationship?.Attribute("Target") ?? throw new InvalidDataException("预报工作表不存在。");
        if (relationship?.Attribute("TargetMode")?.Value == "External" || target.Contains("..")) throw new InvalidDataException("预报工作表关系无效。");
        var path = target.StartsWith('/') ? target.TrimStart('/') : "xl/" + target;
        var rows = new List<Dictionary<int, string>>();
        foreach (var row in Xml(path).Descendants(ns + "row"))
        {
            if (rows.Count >= 100_000) throw new InvalidDataException("预报超过 100000 行，未导入截断数据。");
            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements(ns + "c"))
            {
                int col = 0;
                foreach (char c in (string?)cell.Attribute("r") ?? "") { if (c is < 'A' or > 'Z') break; col = checked(col * 26 + c - 'A' + 1); }
                string text = (string?)cell.Element(ns + "v") ?? "";
                string type = (string?)cell.Attribute("t") ?? "";
                if (type == "inlineStr") text = string.Concat(cell.Descendants(ns + "t").Select(t => t.Value));
                if (type == "s") text = int.TryParse(text, out int index) && index >= 0 && index < strings.Length ? strings[index] : throw new InvalidDataException("预报共享字符串无效。");
                if (type is "n" or "" && text.Contains('E', StringComparison.OrdinalIgnoreCase)
                    && decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)) text = number.ToString("0", CultureInfo.InvariantCulture);
                cells[col] = text.Trim();
            }
            rows.Add(cells);
        }
        var header = rows.Take(20).FirstOrDefault(r => r.Values.Contains("退货物流商") && r.Values.Contains("退货运单号"))
            ?? throw new InvalidDataException("未找到退货物流商、退货运单号表头，未更新预报池。");
        int carrierCol = header.First(c => c.Value == "退货物流商").Key, numberCol = header.First(c => c.Value == "退货运单号").Key;
        int rmaCol = header.FirstOrDefault(c => c.Value.Contains("RMA", StringComparison.OrdinalIgnoreCase)).Key;
        var results = new Dictionary<string, BluettiForecast>();
        foreach (var row in rows.Skip(rows.IndexOf(header) + 1))
        {
            if (!row.Values.Any(v => v.Length > 0)) continue;
            string rawCarrier = row.GetValueOrDefault(carrierCol, ""), number = row.GetValueOrDefault(numberCol, ""), rma = row.GetValueOrDefault(rmaCol, "");
            string carrier = rawCarrier.Equals("FedEx", StringComparison.OrdinalIgnoreCase) ? "fedex" : rawCarrier.Equals("USPS", StringComparison.OrdinalIgnoreCase) ? "usps" : rawCarrier;
            string issue = carrier is not ("fedex" or "usps") ? "承运商缺失或暂不支持" : "";
            try { number = CarrierTrackingClient.Number(number); } catch (ArgumentException) { issue = "运单号缺失或格式无效"; }
            if (rma.Length > 200 || number.Length > 200 || carrier.Length > 40) throw new InvalidDataException("预报字段长度超过限制，未更新数据。");
            string json = JsonSerializer.Serialize(row.ToDictionary(c => c.Key.ToString(), c => c.Value));
            string identity = rma.Length > 0 ? JsonSerializer.Serialize(new[] { rma, carrier, number }) : json;
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
            // A package/RMA can occupy several product rows; preserve them all.
            var source = results.TryGetValue(key, out var existing)
                ? System.Text.Json.Nodes.JsonNode.Parse(existing.SourceJson)!.AsArray() : new System.Text.Json.Nodes.JsonArray();
            source.Add(System.Text.Json.Nodes.JsonNode.Parse(json));
            results[key] = new(key, rma, carrier, number, issue, source.ToJsonString());
        }
        return results.Values.ToArray();
    }
}
