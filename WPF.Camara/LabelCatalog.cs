namespace WPF.Camara;

public sealed class LabelItem(string sku, string checkCodes, int quantity)
{
    public string Sku { get; } = sku;
    public string CheckCodes { get; } = checkCodes;
    public int Quantity { get; set; } = quantity;
}

public sealed record LabelJob(string Sku, int Copies);

public static class LabelCatalog
{
    public static List<LabelItem> Create() => LabelDataArchive.LoadDefault().CreateCatalog();

    public static IReadOnlyList<LabelJob> BuildJobs(IEnumerable<LabelItem> items)
    {
        var rows = items.ToList();
        if (rows.Any(x => x.Quantity < 0 || x.Quantity > 999))
            throw new ArgumentException("每项数量须为 0–999 的整数。");
        return rows.Where(x => x.Quantity > 0).Select(x => new LabelJob(x.Sku, x.Quantity)).ToList();
    }
}

