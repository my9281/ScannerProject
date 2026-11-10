using Scanner.Helpers.Services;
using Scanner.WPF.Helpers;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Xml.Linq;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "BatchLabelTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace pkg = "http://schemas.openxmlformats.org/package/2006/relationships";
        void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        XElement Cell(string reference, string value) => new XElement(ns + "c", new XAttribute("r", reference), new XAttribute("t", "inlineStr"), new XElement(ns + "is", new XElement(ns + "t", value)));
        XElement Row(int r, string sku, string sn, string treatment, string remark, string qty) => new XElement(ns + "row", new XAttribute("r", r), Cell("A" + r, remark), Cell("B" + r, sn), Cell("C" + r, qty), Cell("D" + r, sku), Cell("E" + r, treatment));
        try
        {
            var input = System.IO.Path.Combine(folder, "labels.xlsx");
            using (var zip = ZipFile.Open(input, ZipArchiveMode.Create))
            {
                void Write(string name, XElement root) { using var stream = zip.CreateEntry(name).Open(); new XDocument(root).Save(stream); }
                Write("xl/workbook.xml", new XElement(ns + "workbook", new XElement(ns + "sheets", new XElement(ns + "sheet", new XAttribute("name", "标签"), new XAttribute(rel + "id", "rId1")), new XElement(ns + "sheet", new XAttribute("name", "空表"), new XAttribute(rel + "id", "rId2")))));
                Write("xl/_rels/workbook.xml.rels", new XElement(pkg + "Relationships", new XElement(pkg + "Relationship", new XAttribute("Id", "rId1"), new XAttribute("Target", "worksheets/sheet1.xml")), new XElement(pkg + "Relationship", new XAttribute("Id", "rId2"), new XAttribute("Target", "worksheets/sheet2.xml"))));
                var data = new XElement(ns + "sheetData", Row(1, "sku", "SN", "处理", "备注", "数量"), Row(2, "P-AC180-US-GY", "000123", "报废 ", "不应出现在标签上的原备注", "2.0"), Row(3, "P-AC180-US-GY", "000124", "维修", "更换零件", "1"), Row(4, "P-AC180-US-GY", "000125", "检测", "", ""), Row(5, "SKU", "", "维修", "缺SN", "1"), Row(6, "SKU", "SN", "维修", "数量为零", "0"), Row(7, "SKU", "SN", "维修", "数量为小数", "1.5"), Row(8, "", "", "", "", ""));
                data.Add(Row(9, "", "", "", "合计", "4"));
                Write("xl/worksheets/sheet1.xml", new XElement(ns + "worksheet", data));
                Write("xl/worksheets/sheet2.xml", new XElement(ns + "worksheet", new XElement(ns + "sheetData")));
            }
            var before = File.ReadAllBytes(input);
            var rows = BatchLabelImportService.Load(input);
            Check(before.SequenceEqual(File.ReadAllBytes(input)), "Import must not change source");
            Check(rows.Count == 6 && rows.Count(r => r.Error == null) == 3 && rows.Where(r => r.Error == null).Sum(r => r.Quantity) == 4, "Rows, quantities or error handling");
            Check(rows[0].Sn == "000123" && rows[0].IsScrapped && rows[0].LabelRemark == "报废 / Scrapped / Desechado", "Identifier or scrap override");
            Check(rows[1].LabelRemark == "更换零件" && !rows[1].IsScrapped && rows[2].Remark == "", "Normal and blank remark behavior");
            var method = typeof(PrintingHelper).GetMethod("CreateSkuSerialNumberLabel", BindingFlags.NonPublic | BindingFlags.Static)!;
            var scrap = (Canvas)method.Invoke(null, new object[] { rows[0].Sku, rows[0].Sn, rows[0].Remark, true })!;
            var normal = (Canvas)method.Invoke(null, new object[] { rows[1].Sku, rows[1].Sn, rows[1].Remark, false })!;
            string Text(Canvas canvas) => string.Join("\n", canvas.Children.OfType<TextBlock>().Select(t => new TextRange(t.ContentStart, t.ContentEnd).Text));
            Check(Text(scrap).Contains("报废") && Text(scrap).Contains("Scrapped / Desechado") && !Text(scrap).Contains("备注") && !Text(scrap).Contains(rows[0].Remark), "Scrap must replace both remark heading and original content");
            Check(Text(normal).Contains("备注") && Text(normal).Contains("更换零件"), "Normal remarks");
            var crosses = scrap.Children.Cast<UIElement>().TakeLast(2).Cast<Line>().ToArray();
            Check(crosses.All(l => Panel.GetZIndex(l) == int.MaxValue) && crosses.All(l => Math.Abs(l.X2 - l.X1) > 550 && Math.Abs(l.Y2 - l.Y1) > 370), "X must span full label in top layer");
            Check(normal.Children.OfType<Line>().Count() == 7 && scrap.Children.OfType<Line>().Count() == 9, "Scrap-only X");
            IReadOnlyList<BatchLabelRecord> sample = rows;
            if (args.Length >= 1)
            {
                sample = BatchLabelImportService.Load(args[0]);
                Console.WriteLine($"Sample: {sample.Count} rows, {sample.Count(r => r.Error == null)} valid, {sample.Where(r => r.Error == null).Sum(r => r.Quantity)} labels, {sample.Where(r => r.Error == null && r.IsScrapped).Sum(r => r.Quantity)} scrap labels.");
                Check(sample.All(r => r.Error == null), "Supplied sample should import without invalid rows");
            }
            if (args.Length >= 2)
            {
                Directory.CreateDirectory(args[1]);
                void Save(BatchLabelRecord item, string name)
                {
                    var bitmap = PrintingHelper.PreviewSkuSerialNumberLabel(item.Sku, item.Sn, item.Remark, item.IsScrapped);
                    Check(bitmap.PixelWidth > 1000 && bitmap.PixelHeight > 1000, "Print-resolution preview");
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var stream = File.Create(System.IO.Path.Combine(args[1], name)); encoder.Save(stream);
                }
                Save(sample.First(r => r.Error == null && r.IsScrapped), "scrap-label.png");
                Save(sample.FirstOrDefault(r => r.Error == null && !r.IsScrapped) ?? rows[1], "normal-label.png");
            }
            Console.WriteLine("PASS: header mapping, all worksheets, quantities, leading-zero SN, source preservation, invalid rows, blank remarks, scrap override, three languages and full-label topmost X. No print jobs sent.");
        }
        finally { Directory.Delete(folder, true); }
    }
}
