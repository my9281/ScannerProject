namespace WPF.Camara;

public sealed record CameraModel(string Sku, bool Transmitter, bool Detector, bool WhiteBalance, bool Speaker, params string[] Specifications)
{
    public static IReadOnlyList<CameraModel> All { get; } = LabelDataArchive.LoadDefault().CreateModels();

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
