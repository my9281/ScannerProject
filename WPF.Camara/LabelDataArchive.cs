using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace WPF.Camara;

public sealed record ForecastDataRow(int Line, int Pallet, string OriginalCode, int Quantity, string Barcode, string Sku);
public sealed record CatalogDataRow(string Sku, string CheckCodes, int Quantity);
public sealed record ModelDataRow(string Sku, bool Transmitter, bool Detector, bool WhiteBalance, bool Speaker, string[] Specifications);

// Store source values only. Display text, printability and label jobs remain derived by the application.
public sealed record LabelDataArchive(int SchemaVersion, ForecastDataRow[] ForecastRows, CatalogDataRow[] LabelCatalog, ModelDataRow[] CameraModels)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private static readonly Lazy<LabelDataArchive> Default = new(() =>
    {
        using var stream = typeof(LabelDataArchive).Assembly.GetManifestResourceStream("WPF.Camara.Data.label-data.json")
            ?? throw new InvalidDataException("缺少标签基础数据 label-data.json。");
        using var reader = new StreamReader(stream);
        return Parse(reader.ReadToEnd());
    });

    public static LabelDataArchive LoadDefault() => Parse(Default.Value.ToJson());

    public static LabelDataArchive Capture(ReceivingForecast forecast, IEnumerable<LabelItem> catalog, IEnumerable<CameraModel> models) =>
        new(1, forecast.Rows.Select(x => new ForecastDataRow(x.Line, x.Pallet, x.OriginalCode, x.Quantity, x.Barcode, x.Sku)).ToArray(),
            catalog.Select(x => new CatalogDataRow(x.Sku, x.CheckCodes, x.Quantity)).ToArray(),
            models.Select(x => new ModelDataRow(x.Sku, x.Transmitter, x.Detector, x.WhiteBalance, x.Speaker, x.Specifications.ToArray())).ToArray());

    public ReceivingForecast CreateForecast() => new(ForecastRows.Select(x => new ReceivingRow(x.Line, x.Pallet, x.OriginalCode, x.Quantity, x.Barcode, x.Sku)).ToArray());
    public List<LabelItem> CreateCatalog() => LabelCatalog.Select(x => new LabelItem(x.Sku, x.CheckCodes, x.Quantity)).ToList();
    public IReadOnlyList<CameraModel> CreateModels() => CameraModels.Select(x => new CameraModel(x.Sku, x.Transmitter, x.Detector, x.WhiteBalance, x.Speaker, x.Specifications.ToArray())).ToArray();

    public string ToJson()
    {
        Validate();
        return JsonSerializer.Serialize(this, Options) + Environment.NewLine;
    }

    public static LabelDataArchive Parse(string json)
    {
        try
        {
            var data = JsonSerializer.Deserialize<LabelDataArchive>(json, Options) ?? throw new FormatException("标签数据为空。");
            data.Validate();
            return data;
        }
        catch (JsonException ex) { throw new FormatException("标签数据 JSON 格式无效。", ex); }
    }

    public void Save(string path)
    {
        string json = ToJson();
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    private void Validate()
    {
        if (SchemaVersion != 1) throw new FormatException("不支持的标签数据版本，应为 1。");
        if (ForecastRows is not { Length: > 0 } || LabelCatalog is not { Length: > 0 } || CameraModels is not { Length: > 0 })
            throw new FormatException("收货明细、贴标清单及型号规格均不能为空。");
        if (ForecastRows.Any(x => x is null || x.Line <= 0 || x.Pallet == 0 || x.Quantity is < 0 or > 999
                || x.OriginalCode is null || x.Barcode is null || x.Sku is null || x.Barcode.Any(c => c < 32 || c > 126))
            || ForecastRows.Select(x => x.Line).Distinct().Count() != ForecastRows.Length)
            throw new FormatException("收货明细包含无效行号、分组、编码或数量。");
        if (LabelCatalog.Any(x => x is null || string.IsNullOrWhiteSpace(x.Sku) || x.CheckCodes is null || x.Quantity is < 0 or > 999))
            throw new FormatException("贴标清单包含无效 SKU 或数量。");
        if (CameraModels.Any(x => x is null || string.IsNullOrWhiteSpace(x.Sku) || x.Specifications is not { Length: > 0 }
                || x.Specifications.Any(string.IsNullOrWhiteSpace)))
            throw new FormatException("型号规格包含空值。");
    }
}
