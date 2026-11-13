using System.IO;
using System.Reflection;
using WPF.Camara;

internal static class CurrentBatchTests
{
    public static LabelDataArchive History()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Tests.label-history.json")!;
        using var reader = new StreamReader(stream);
        return LabelDataArchive.Parse(reader.ReadToEnd());
    }

    public static void Run()
    {
        var current = LabelDataArchive.LoadDefault();
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Tests.batch2-source.tsv")!;
        using var reader = new StreamReader(stream);
        var source = reader.ReadToEnd().Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => x.TrimEnd('\r').Split('\t')).ToArray();
        Check(source.Length == 66 && current.ForecastRows.Length == 66 && current.LabelCatalog.Length == 66, "second batch only");
        Check(current.ForecastRows.Sum(x => x.Quantity) == 261 && current.LabelCatalog.Sum(x => x.Quantity) == 261, "batch total 261");
        for (int i = 0; i < source.Length; i++)
        {
            var row = current.ForecastRows[i];
            var item = current.LabelCatalog[i];
            Check(row.Line == i + 1 && row.Pallet == -3 && row.OriginalCode == source[i][0] && row.Barcode == source[i][0]
                && row.Sku == source[i][1] && row.Quantity == int.Parse(source[i][2]), "all supplied forecast values preserved at line " + (i + 1));
            Check(item.CheckCodes == row.Barcode && item.Sku == row.Sku && item.Quantity == row.Quantity, "catalog matches batch");
        }
        Check(current.ForecastRows.Count(x => x.Sku == "U8R") == 4, "same SKU with separate FNSKU remains separate");
        Check(current.CreateForecast().BuildJobs(-3, true).Sum(x => x.Copies) == 261
            && current.CreateForecast().BuildJobs(-3, false).Count == 66, "print counts");
        Check(History().ForecastRows.Length == 158 && History().LabelCatalog.Length == 29, "history preserved");
        Check(current.CreateForecast().PalletChoices.Single().Name == "第二批", "second batch display name");
        Console.WriteLine("PASS: second batch 66 rows / 261 labels, exact input values, independent FNSKU rows and historical snapshot.");
    }
    private static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
