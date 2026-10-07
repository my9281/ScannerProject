using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Scanner.CheckListBoard.Models;

namespace Scanner.CheckListBoard.Services;

public sealed class WorkOrderStore(string directory, string accountKey)
{
    private string PathName => Path.Combine(directory, $"workorders-test-{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accountKey)))}.json");
    public async Task<IReadOnlyList<WorkOrderRow>> LoadAsync()
    {
        if (!File.Exists(PathName)) return CreateSamples();
        await using var stream = File.OpenRead(PathName);
        var rows = await JsonSerializer.DeserializeAsync<List<WorkOrderRow>>(stream);
        return rows ?? throw new InvalidDataException("保存的工单数据无效。");
    }
    public async Task SaveAsync(IEnumerable<WorkOrderRow> rows)
    {
        Directory.CreateDirectory(directory);
        string temporary = PathName + ".tmp";
        try
        {
            await using (var stream = File.Create(temporary))
                await JsonSerializer.SerializeAsync(stream, rows.ToArray());
            File.Move(temporary, PathName, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    public static IReadOnlyList<WorkOrderRow> CreateSamples()
    {
        string[] descriptions = ["检查设备外观与机身完整性", "核对设备序列号和标签", "检查电源及开机状态", "检测屏幕显示和触控功能", "检查接口与连接稳定性", "核验安全检测项目", "检测电池及充电功能", "检查设备运行噪声", "复核检测评级与记录", "完成出库前最终检查"];
        return descriptions.Select((description, index) => new WorkOrderRow
        {
            Number = index + 1, DeviceId = $"TEST-DEV-{index + 1:000}", Description = description,
            IsCompleted = index is 1 or 4 or 7
        }).ToArray();
    }
}
