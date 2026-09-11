using Scanner.Models.MauiContracts;
using System.Text;

namespace Scanner.MaUI.Services;

public sealed class CsvImportService
{
    private const int UrgentColumn = 9;
    private const int SnColumn = 24;
    private const int TrackingColumn = 29;
    private const int RemarkColumn = 43;
    public async Task<IReadOnlyList<WorkOrderRemark>> ImportAsync(FileResult file)
    {
        string extension = Path.GetExtension(file.FileName);
        if (extension.Equals(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            string path = Path.Combine(FileSystem.CacheDirectory, $"urgent_{Guid.NewGuid():N}.xlsx");
            await using (Stream input = await file.OpenReadAsync()) await using (FileStream output = File.Create(path)) await input.CopyToAsync(output);
            Scanner.Helpers.UrgentWorkOrderImportResult imported = new Scanner.Helpers.UrgentWorkOrderImportHelper().Import(path);
            return imported.Rules.Select(x => new WorkOrderRemark { Id=x.Id, Sn=x.Sn, TrackingNumber=x.TrackingNumber, Remark=x.Remark, RemarkTimestamp=x.RemarkTimestamp, IsUrgent=x.IsUrgent, IsRepair=x.IsRepair, IsOidRule=x.IsOidRule }).ToList();
        }
        await using Stream stream = await file.OpenReadAsync();
        return await ImportAsync(stream);
    }
    public async Task<IReadOnlyList<WorkOrderRemark>> ImportAsync(Stream stream)
    {
        using StreamReader reader = new(stream, Encoding.UTF8, true, leaveOpen: true);
        List<WorkOrderRemark> result = new();
        int rowNumber = 0;
        while (await reader.ReadLineAsync() is string line)
        {
            rowNumber++;
            List<string> fields = ParseLine(line);
            if (fields.Count < 44 || rowNumber == 1 && IsHeader(fields)) continue;
            if (!fields[UrgentColumn].Contains("维修", StringComparison.OrdinalIgnoreCase)) continue;
            string sn = fields[SnColumn].Trim();
            string tracking = fields[TrackingColumn].Trim();
            if (string.IsNullOrWhiteSpace(sn) && string.IsNullOrWhiteSpace(tracking)) throw new InvalidOperationException($"CSV 第 {rowNumber} 行的 SN 和运单号不能同时为空。");
            result.Add(new WorkOrderRemark { Id = $"CSV-{rowNumber}-{Guid.NewGuid():N}", Sn = EmptyToNull(sn), TrackingNumber = EmptyToNull(tracking), Remark = fields[RemarkColumn].Trim(), IsUrgent = true, IsRepair = true, IsOidRule = string.IsNullOrWhiteSpace(sn) && !string.IsNullOrWhiteSpace(tracking), RemarkTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() });
        }
        return result;
    }
    private static List<string> ParseLine(string line)
    {
        List<string> fields = new();
        StringBuilder field = new();
        bool quoted = false;
        for (int index = 0; index < line.Length; index++)
        {
            char character = line[index];
            if (character == '"' && quoted && index + 1 < line.Length && line[index + 1] == '"')
            {
                field.Append('"');
                index++;
            }
            else if (character == '"') quoted = !quoted;
            else if (character == ',' && !quoted)
            {
                fields.Add(field.ToString());
                field.Clear();
            }
            else field.Append(character);
        }
        if (quoted) throw new InvalidOperationException("CSV 包含未闭合的引号。");
        fields.Add(field.ToString());
        return fields;
    }
    private static bool IsHeader(IReadOnlyList<string> fields) => fields[SnColumn].Contains("SN", StringComparison.OrdinalIgnoreCase) || fields[TrackingColumn].Contains("Tracking", StringComparison.OrdinalIgnoreCase) || fields[TrackingColumn].Contains("运单");
    private static string? EmptyToNull(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
