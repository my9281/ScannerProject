using Scanner.Models.MauiContracts;

namespace Scanner.MaUI.Services;

// Share WPF import rules; only file access belongs to MAUI.
public sealed class CsvImportService
{
    public async Task<IReadOnlyList<WorkOrderRemark>> ImportAsync(FileResult file)
    {
        string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension is not (".csv" or ".xlsx"))
            throw new InvalidOperationException("仅支持 .xlsx 或 .csv 预报文件。");
        await using Stream input = await file.OpenReadAsync();
        return await ImportAsync(input, extension);
    }

    public Task<IReadOnlyList<WorkOrderRemark>> ImportAsync(Stream stream) => ImportAsync(stream, ".csv");

    private static async Task<IReadOnlyList<WorkOrderRemark>> ImportAsync(Stream stream, string extension)
    {
        string path = Path.Combine(FileSystem.CacheDirectory, $"forecast_{Guid.NewGuid():N}{extension}");
        try
        {
            await using (FileStream output = File.Create(path))
                await stream.CopyToAsync(output);
            var imported = new Scanner.Helpers.UrgentWorkOrderImportHelper().Import(path);
            return imported.Rules.Select(x => new WorkOrderRemark
            {
                Id = x.Id, Sn = x.Sn, TrackingNumber = x.TrackingNumber,
                Remark = x.Remark, RemarkTimestamp = x.RemarkTimestamp,
                IsUrgent = x.IsUrgent, IsRepair = x.IsRepair,
                IsOidRule = x.IsOidRule
            }).ToList();
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
