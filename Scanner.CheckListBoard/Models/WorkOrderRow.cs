using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Scanner.CheckListBoard.Models;

public sealed class WorkOrderRow : INotifyPropertyChanged
{
    public ulong Id { get; set; }
    public ulong UserId { get; set; }
    public uint Version { get; set; }
    public DateTimeOffset TaskAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public int Number { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    private bool isCompleted;
    public bool IsCompleted
    {
        get => isCompleted;
        set
        {
            if (isCompleted == value) return;
            isCompleted = value;
            Changed(); Changed(nameof(Completion)); Changed(nameof(StatusText));
        }
    }
    [JsonIgnore] public int Completion => IsCompleted ? 1 : 0;
    [JsonIgnore] public string StatusText => IsCompleted ? "已完成" : "未完成";
    [JsonIgnore] public bool CanEdit { get; set; } = true;
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}
