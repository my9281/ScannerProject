using System.IO;
using System.Text.RegularExpressions;

namespace WPF.Camara;

public sealed record ReceivingRow(int Line, int Pallet, string OriginalCode, int Quantity, string Barcode, string Sku)
{
    public string PalletName => ReceivingForecast.PalletName(Pallet);
    public bool CanPrint => Quantity > 0 && Sku != "#N/A" && Sku.Length > 0 && Barcode.Length > 0 && Barcode != "Unconfirmed";
    public string State => !CanPrint ? "跳过：SKU/条码未确认或数量为0" : OriginalCode != Barcode ? "使用表内校对码；请核对原码" : Barcode.StartsWith("LPN", StringComparison.OrdinalIgnoreCase) ? "LPN码；按表保留" : "已知SKU";
}

public sealed class ReceivingForecast(IReadOnlyList<ReceivingRow> rows)
{
    public IReadOnlyList<ReceivingRow> Rows { get; } = rows;
    public IReadOnlyList<int> Pallets => Rows.Select(x => x.Pallet).Distinct().ToArray();
    public IEnumerable<ReceivingPallet> PalletChoices => Pallets.Select(x => new ReceivingPallet(x, PalletName(x)));
    public static string PalletName(int pallet) => pallet < 0 ? $"散货{-pallet}" : $"第 {pallet} 板";
    public static ReceivingForecast LoadDefault()
    {
        var rows = new List<ReceivingRow>();
        foreach (var name in new[] { "ReceivingForecast.tsv", "LooseGoods1.tsv", "LooseGoods2.tsv" })
        {
            using var stream = typeof(ReceivingForecast).Assembly.GetManifestResourceStream($"WPF.Camara.Data.{name}")!;
            using var reader = new StreamReader(stream);
            int offset = rows.Count;
            rows.AddRange(Parse(reader.ReadToEnd()).Rows.Select(x => x with { Line = x.Line + offset }));
        }
        return new(rows);
    }

    public static ReceivingForecast Parse(string text)
    {
        var rows = new List<ReceivingRow>();
        int pallet = 0, line = 0;
        foreach (var rawLine in text.Split('\n'))
        {
            line++;
            if (string.IsNullOrWhiteSpace(rawLine)) continue;
            var cells = rawLine.TrimEnd('\r').Split('\t').Select(x => x.Trim()).ToArray();
            if (cells.Length is not (5 or 7)) throw new FormatException($"第 {line} 行应为 5 或 7 列，请使用制表符分隔的原始预报。");
            if (cells[0].Length > 0)
            {
                var loose = Regex.Match(cells[0], @"^散货([1-9]\d*)$");
                var match = Regex.Match(cells[0], @"^第(.+?)个卡板$");
                if (loose.Success && int.TryParse(loose.Groups[1].Value, out int looseNumber))
                    pallet = -looseNumber;
                else
                {
                    if (!match.Success) throw new FormatException($"第 {line} 行的卡板名称无效。");
                    pallet = match.Groups[1].Value switch
                    {
                        "一" => 1, "二" => 2, "三" => 3, "四" => 4, "五" => 5, "六" => 6,
                        "七" => 7, "八" => 8, "九" => 9, "十" => 10,
                        var value when int.TryParse(value, out int number) && number > 0 => number,
                        _ => throw new FormatException($"第 {line} 行的卡板编号无法识别。")
                    };
                }
            }
            if (pallet == 0 || !int.TryParse(cells[2], out int qty) || qty < 0 || qty > 999)
                throw new FormatException($"第 {line} 行缺少卡板或数量无效（0–999）。");
            // Column 4 is the supplied corrected code. Never infer a replacement from a similar code.
            if (cells[3].Any(c => c < 32 || c > 126)) throw new FormatException($"第 {line} 行条码含不支持的字符。");
            rows.Add(new(line, pallet, cells[1], qty, cells[3], cells[4]));
        }
        if (rows.Count == 0) throw new FormatException("预报表没有数据。");
        return new(rows);
    }

    public IReadOnlyList<ReceivingLabelJob> BuildJobs(int pallet, bool perPiece) => Rows
        .Where(x => x.Pallet == pallet && x.CanPrint)
        .Select(x => new ReceivingLabelJob(x, perPiece ? x.Quantity : 1, perPiece)).ToArray();
}

public sealed record ReceivingLabelJob(ReceivingRow Row, int Copies, bool PerPiece);
public sealed record ReceivingPallet(int Number, string Name);
