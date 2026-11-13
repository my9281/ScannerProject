using System.IO;
using System.Text.Json.Nodes;
using WPF.Camara;

internal static class LabelDataTests
{
    public static void Run()
    {
        var data = CurrentBatchTests.History();
        var original = new List<ReceivingRow>();
        foreach (var file in new[] { "ReceivingForecast.tsv", "LooseGoods1.tsv", "LooseGoods2.tsv" })
        {
            using var stream = typeof(ReceivingForecast).Assembly.GetManifestResourceStream("WPF.Camara.Data." + file)!;
            using var reader = new StreamReader(stream);
            int offset = original.Count;
            original.AddRange(ReceivingForecast.Parse(reader.ReadToEnd()).Rows.Select(x => x with { Line = x.Line + offset }));
        }
        Check(original.SequenceEqual(data.CreateForecast().Rows), "JSON must preserve every TSV value and row order");
        Check(data.ForecastRows.Length == 158 && data.ForecastRows.Sum(x => x.Quantity) == 621
            && data.LabelCatalog.Length == 29 && data.CameraModels.Length == 14, "complete source data coverage");
        var decoded = LabelDataArchive.Parse(data.ToJson());
        Check(decoded.ToJson() == data.ToJson(), "roundtrip including model flags and specifications");
        var catalog = data.CreateCatalog();
        catalog[0].Quantity = 9;
        var edited = LabelDataArchive.Capture(data.CreateForecast(), catalog, data.CreateModels());
        Check(LabelDataArchive.Parse(edited.ToJson()).CreateCatalog()[0].Quantity == 9, "current edited quantities exported");
        Check(LabelDataArchive.LoadDefault().CreateCatalog()[0].Quantity == 1, "export does not overwrite defaults");
        var model = data.CreateModels()[0];
        model.Specifications[0] = "TEST";
        Check(LabelDataArchive.LoadDefault().CameraModels[0].Specifications[0] != "TEST", "model arrays isolated");
        foreach (string field in new[] { "schemaVersion", "forecastRows", "labelCatalog", "cameraModels" })
        {
            var bad = JsonNode.Parse(data.ToJson())!;
            bad[field] = field == "schemaVersion" ? JsonValue.Create(99) : null;
            MustReject(bad.ToJsonString());
        }
        var invalidQty = JsonNode.Parse(data.ToJson())!;
        invalidQty["forecastRows"]![0]!["quantity"] = -1;
        MustReject(invalidQty.ToJsonString());
        var duplicateLine = JsonNode.Parse(data.ToJson())!;
        duplicateLine["forecastRows"]![1]!["line"] = duplicateLine["forecastRows"]![0]!["line"]!.DeepClone();
        MustReject(duplicateLine.ToJsonString());
        Directory.CreateDirectory("artifacts/camara");
        const string path = "artifacts/camara/label-data-roundtrip.json";
        edited.Save(path);
        Check(LabelDataArchive.Parse(File.ReadAllText(path)).ToJson() == edited.ToJson(), "saved UTF-8 JSON reloads");
        Console.WriteLine("PASS: label JSON retains all source rows, codes, quantities, catalog and model specifications; edited export, roundtrip and validation.");
    }

    private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void MustReject(string json)
    {
        try { LabelDataArchive.Parse(json); }
        catch (FormatException) { return; }
        throw new Exception("Invalid archive accepted");
    }
}
