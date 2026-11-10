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
    public static List<LabelItem> Create() =>
    [
        new("F5100DJKABTX-40M-7C", "X004506HCH", 6),
        new("F5100DJKABTX-50M-5C", "X0039CX5SB, X004TQYVTN", 2),
        new("F5100DJKABTX-70M-5C", "X0046T3QXZ, X0039E88Z9", 4),
        new("F5800ADJKABTX-100M-5C", "X003GKPKFH", 3),
        new("F5800ADJKABTX-50M-7C", "X0039E9BR3", 1),
        new("S840DSM-5C-30M", "X004L3M0M9", 11),
        new("S840DSM-5C-50M", "X004KXUHAR", 5),
        new("S840DSM-5C-70M", "X004L5SIJB", 3),
        new("S840DSRTMS68-7C-100M", "X004TMTW8R", 3),
        new("S840DSRTMS68-7C-50M", "X004IJV3IX", 17),
        new("S840DSRTMS68-7C-70M", "X004IJHF6H", 23),
        new("S850DSTKM-7C-30M", "X004SMN347", 3),
        new("S850DSTKM-7C-50M", "X004SMTNWN", 4),
        new("S850DSTKMS68-7C-30M", "X00528RY5F", 3),
        new("S850DSTKMS68-7C-50M", "X0053XZ3F7", 3),
        new("S860DSTKM-7C-30M", "X004S5PMT3", 0),
        new("S860DSTKM-7C-50M", "X004S5X9NT", 3),
        new("S860DSTKMS68-7C-30M", "X0053XZ5B9", 0),
        new("S8951DM-30M", "X003VLS3ZP", 14),
        new("S8951DM-50M", "X003VLERJ1", 10),
        new("S8951DSRTMS68-20M", "X004HIRJCJ", 19),
        new("S8951DSRTMS68-30M", "X004HIO7LP", 33),
        new("S8951DSRTMS68-50M", "X004HIRJF1", 30),
        new("S8951DSTM-20M", "X004HITGE3", 4),
        new("S8951DSTM-30M", "X004HIQ0BZ", 8),
        new("S8951DSTM-50M", "X004HIVEQV", 8),
        new("S68R", "X004JKM1EV, X004HFUJND, X004P3HPUH", 33),
        new("S840DSM-7C-100M", "", 0),
        new("F5800ADJKABTX-70M-5C", "", 0)
    ];

    public static IReadOnlyList<LabelJob> BuildJobs(IEnumerable<LabelItem> items)
    {
        var rows = items.ToList();
        if (rows.Any(x => x.Quantity < 0 || x.Quantity > 999))
            throw new ArgumentException("每项数量须为 0–999 的整数。");
        return rows.Where(x => x.Quantity > 0).Select(x => new LabelJob(x.Sku, x.Quantity)).ToList();
    }
}

