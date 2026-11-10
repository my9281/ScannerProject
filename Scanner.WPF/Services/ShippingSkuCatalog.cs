using System;
using System.Collections.Generic;
using System.Globalization;
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
        public string MaterialNumber { get; }
        public string MaterialName { get; }
        public string Version { get; }
        public string Upc { get; }
        public string Ean { get; }
        public string Jan { get; }
        public string Specification { get; }
        public string Length { get; }
        public string Width { get; }
        public string Height { get; }
        public string Weight { get; }
        public decimal? LengthInches { get; }
        public decimal? WidthInches { get; }
        public decimal? HeightInches { get; }
        public decimal? WeightPounds { get; }
        public bool HasShippingMeasurements => LengthInches.HasValue || WidthInches.HasValue ||
            HeightInches.HasValue || WeightPounds.HasValue;
        public string Details { get; }
        internal string SearchText { get; }
        internal ShippingSkuEntry(string[] headers, string[] cells)
        {
            Sku = cells[0]; Description = cells[1]; Category = cells[2]; Model = cells[14];
            MaterialNumber = cells[3]; MaterialName = cells[5]; Version = cells[15];
            Upc = cells[22]; Ean = cells[23]; Jan = cells[24]; Specification = cells[25];
            Length = cells[10]; Width = cells[11]; Height = cells[12]; Weight = cells[13];
            LengthInches = ConvertAndRoundUp(Length, 2.54m);
            WidthInches = ConvertAndRoundUp(Width, 2.54m);
            HeightInches = ConvertAndRoundUp(Height, 2.54m);
            WeightPounds = ConvertAndRoundUp(Weight, 0.45359237m);
            Details = string.Join(Environment.NewLine, headers.Select((h, i) => h + "：" + (cells[i].Length == 0 ? "—" : cells[i])));
            SearchText = ShippingSkuCatalog.Normalize(string.Join(" ", cells));
        }

        private static decimal? ConvertAndRoundUp(string value, decimal metricUnitsPerImperialUnit)
        {
            if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number <= 0)
                return null;
            return decimal.Ceiling(number / metricUnitsPerImperialUnit);
        }
    }

    public static class ShippingSkuCatalog
    {
        private static readonly Lazy<IReadOnlyList<ShippingSkuEntry>> Data = new Lazy<IReadOnlyList<ShippingSkuEntry>>(Load);
        public static IReadOnlyList<ShippingSkuEntry> Entries => Data.Value;
        internal static string Normalize(string value) => value.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        public static ShippingSkuEntry[] Search(string query)
        {
            var terms = Normalize(query ?? "").Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
            return Entries.Where(row => terms.All(term => row.SearchText.Contains(term)))
                .OrderByDescending(row => string.Equals(row.Sku, (query ?? "").Trim(), StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        private static IReadOnlyList<ShippingSkuEntry> Load()
        {
            using var stream = typeof(ShippingSkuCatalog).Assembly.GetManifestResourceStream("Scanner.Shipping.SkuCatalog.xlsx")
                ?? throw new InvalidDataException("未找到 SKU 发货参考表。");
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XDocument Read(string name)
            {
                using var input = (zip.GetEntry(name) ?? throw new InvalidDataException(name)).Open();
                return XDocument.Load(input);
            }
            var strings = Read("xl/sharedStrings.xml").Elements(ns + "sst").Elements(ns + "si")
                .Select(si => string.Concat(si.Descendants(ns + "t").Select(t => t.Value))).ToArray();
            var rows = Read("xl/worksheets/sheet1.xml").Descendants(ns + "row").Select(row =>
            {
                var cells = new string[36]; Array.Fill(cells, "");
                foreach (var cell in row.Elements(ns + "c"))
                {
                    int col = 0;
                    foreach (char c in cell.Attribute("r")?.Value ?? "") { if (c < 'A' || c > 'Z') break; col = col * 26 + c - 'A' + 1; }
                    if (col < 1 || col > cells.Length) continue;
                    string value = cell.Element(ns + "v")?.Value ?? "";
                    cells[col - 1] = cell.Attribute("t")?.Value == "s" ? strings[int.Parse(value)]
                        : cell.Attribute("t")?.Value == "inlineStr" ? string.Concat(cell.Descendants(ns + "t").Select(t => t.Value)) : value;
                }
                return cells;
            }).ToArray();
            return Array.AsReadOnly(rows.Skip(1).Where(row => row[0].Length > 0).Select(row => new ShippingSkuEntry(rows[0], row)).ToArray());
        }
    }
}
