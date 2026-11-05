using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace Scanner.WPF.Services
{
    public sealed class ShippingSkuEntry
    {
        public string Sku { get; }
        public string Description { get; }
        public string Category { get; }
        public string Model { get; }
        public string Length { get; }
        public string Width { get; }
        public string Height { get; }
        public string Weight { get; }
        public string Details { get; }
        internal string SearchText { get; }
        internal ShippingSkuEntry(string[] headers, string[] cells)
        {
            Sku = cells[0]; Description = cells[1]; Category = cells[2]; Model = cells[14];
            Length = cells[10]; Width = cells[11]; Height = cells[12]; Weight = cells[13];
            Details = string.Join(Environment.NewLine, headers.Select((h, i) => h + "：" + (cells[i].Length == 0 ? "—" : cells[i])));
            SearchText = ShippingSkuCatalog.Normalize(string.Join(" ", cells));
        }
    }

    public static class ShippingSkuCatalog
    {
        private static readonly Lazy<IReadOnlyList<ShippingSkuEntry>> Data = new Lazy<IReadOnlyList<ShippingSkuEntry>>(Load);
        public static IReadOnlyList<ShippingSkuEntry> Entries => Data.Value;
        internal static string Normalize(string value) => value.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        public static ShippingSkuEntry[] Search(string query)
        {
            var terms = Normalize(query ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return Entries.Where(row => terms.All(term => row.SearchText.Contains(term)))
                .OrderByDescending(row => string.Equals(row.Sku, (query ?? "").Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        private static IReadOnlyList<ShippingSkuEntry> Load()
        {
            using var stream = typeof(ShippingSkuCatalog).Assembly.GetManifestResourceStream("Scanner.Shipping.SkuCatalog.xlsx")
                ?? throw new InvalidDataException("未找到 SKU 发货参考表。");
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XDocument Read(string name) { using var input = zip.GetEntry(name).Open(); return XDocument.Load(input); }
            var strings = Read("xl/sharedStrings.xml").Root.Elements(ns + "si")
                .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value))).ToArray();
            var rows = Read("xl/worksheets/sheet1.xml").Descendants(ns + "row").Select(row =>
            {
                var cells = new string[36]; Array.Fill(cells, "");
                foreach (var cell in row.Elements(ns + "c"))
                {
                    int col = 0;
                    foreach (char c in (string)cell.Attribute("r")) { if (c < 'A' || c > 'Z') break; col = col * 26 + c - 'A' + 1; }
                    if (col < 1 || col > cells.Length) continue;
                    string value = (string)cell.Element(ns + "v") ?? "";
                    cells[col - 1] = (string)cell.Attribute("t") == "s" ? strings[int.Parse(value)]
                        : (string)cell.Attribute("t") == "inlineStr" ? string.Concat(cell.Descendants(ns + "t").Select(t => t.Value)) : value;
                }
                return cells;
            }).ToArray();
            return Array.AsReadOnly(rows.Skip(1).Where(row => row[0].Length > 0).Select(row => new ShippingSkuEntry(rows[0], row)).ToArray());
        }
    }
}
