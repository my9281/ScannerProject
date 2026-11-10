namespace WPF.Camara;

public sealed record CameraModel(string Sku, bool Transmitter, bool Detector, bool WhiteBalance, bool Speaker, params string[] Specifications)
{
    public static IReadOnlyList<CameraModel> All { get; } =
    [
        new("S8981DM", false, false, false, false, "5C-20M", "5C-30M", "5C-50M"),
        new("S8981DSTM", true, false, true, false, "5C-20M", "5C-30M", "5C-50M"),
        new("S8981DSRTMS68", true, true, true, false, "5C-20M", "5C-30M", "5C-50M"),
        new("S8951DM", false, false, false, false, "20M", "30M", "50M"),
        new("S8951DSTM", true, false, true, false, "20M", "30M", "50M"),
        new("S8951DSRTMS68", true, true, true, false, "20M", "30M", "50M"),
        new("S840DSM", false, false, true, true, "5C-50M", "5C-70M", "7C-100M"),
        new("S840DSRTMS68", true, true, true, true, "7C-50M", "7C-70M", "7C-100M"),
        new("F5800ADJKABTX", true, false, true, true, "70M-5C", "100M-5C", "50M-7C"),
        new("F5100DJKABTX", true, false, true, true, "40M-7C", "50M-5C", "70M-5C"),
        new("S850DSTKM", true, false, true, true, "7C-30M", "7C-50M"),
        new("S850DSTKMS68", true, true, true, true, "7C-30M", "7C-50M"),
        new("S860DSTKM", true, false, true, true, "7C-30M", "7C-50M"),
        new("S860DSTKMS68", true, true, true, true, "7C-30M", "7C-50M")
    ];

    public IReadOnlyList<string> VariantSkus => Specifications.Select(x => $"{Sku}-{x}").ToArray();

    public string DescribeSku(string fullSku)
    {
        var parts = fullSku.Split('-').Skip(1).ToArray();
        var cable = parts.FirstOrDefault(x => x.EndsWith('C'))?.TrimEnd('C') ?? "5";
        var meters = parts.FirstOrDefault(x => x.EndsWith('M'))?.TrimEnd('M') ?? "/";
        return $"麦克风：有    扬声器：{Mark(Speaker)}\n录像：有    计米：有\n512Hz 发射器：{Mark(Transmitter)}    512Hz 探测器：{Mark(Detector)}\n白平衡：{Mark(WhiteBalance)}    摄像头直径：23 mm\n电缆直径：{cable} mm    米数：{meters} 米";
    }

    public static string Describe(string fullSku)
    {
        var model = All.OrderByDescending(x => x.Sku.Length).FirstOrDefault(x => fullSku.StartsWith(x.Sku + "-", StringComparison.Ordinal));
        return model?.DescribeSku(fullSku) ?? "此 SKU 暂无配置说明。";
    }
    private static string Mark(bool value) => value ? "有" : "/";
}
