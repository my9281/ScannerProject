using System.Text;

namespace MaUIScanner.Services;

public sealed class SpeechService
{
    private static readonly Dictionary<char, string> Digits = new() { ['0'] = "零", ['1'] = "一", ['2'] = "二", ['3'] = "三", ['4'] = "四", ['5'] = "五", ['6'] = "六", ['7'] = "七", ['8'] = "八", ['9'] = "九" };
    public Task SpeakOidAsync() => TextToSpeech.Default.SpeakAsync("O I D");
    public Task SpeakGs1AreaWarningAsync() => TextToSpeech.Default.SpeakAsync("警告，地区码");
    public Task SpeakTailAsync(string serialNumber)
    {
        string value = serialNumber.Length > 5 ? serialNumber[^5..] : serialNumber;
        string text = string.Concat(value.Select(character => Digits.TryGetValue(character, out string? digit) ? digit : character.ToString()));
        return TextToSpeech.Default.SpeakAsync(text);
    }
}

public sealed class ScanLogService
{
    public ScanLogService()
    {
        string folder = Path.Combine(FileSystem.AppDataDirectory, "logs");
        FilePath = Path.Combine(folder, "scanned_codes.txt");
        ErrorPath = Path.Combine(folder, "error.log");
    }
    public string FilePath { get; }
    public string ErrorPath { get; }
    public async Task AppendAsync(string code)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        await File.AppendAllTextAsync(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}\t{code}{Environment.NewLine}", new UTF8Encoding(true));
    }
    public async Task WriteErrorAsync(Exception exception)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ErrorPath)!);
        await File.AppendAllTextAsync(ErrorPath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
    }
    public async Task OpenAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        if (!File.Exists(FilePath)) await File.WriteAllTextAsync(FilePath, string.Empty);
        await Launcher.Default.OpenAsync(new OpenFileRequest("Scan log", new ReadOnlyFile(FilePath)));
    }
}
