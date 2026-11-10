// Isolate classification tests from speech devices and the user's scan log.
namespace Scanner.WPF.Services
{
    public sealed class SpeechService : IDisposable
    {
        public void SpeakOid() { }
        public void Dispose() { }
    }
}

namespace Scanner.WPF.Helpers
{
    public sealed class ScanLogHelper
    {
        public ScanLogHelper() { }
        public ScanLogHelper(bool replacementMode) { }
        public string FilePath => "in-memory";
        public string WorkbookPath => "in-memory.xlsx";
        public void Append(string code) { }
        public void ClearTrackingCache() { }
        public void AppendWorkbook(string code, bool isOid, string selectedModel, string rmaNumber = "") { }
        public void CreateNewFiles() { }
        public void Open() { }
        public void OpenWorkbook() { }
        public void WriteError(Exception exception) { }
    }
}

namespace Scanner.MaUI.Services
{
    public sealed class ScanLogService
    {
        public string FilePath => "in-memory";
        public bool FailNextAppend { get; set; }
        public int AppendCount { get; private set; }
        public Task AppendAsync(string code)
        {
            if (FailNextAppend)
            {
                FailNextAppend = false;
                throw new IOException("Simulated log failure");
            }
            AppendCount++;
            return Task.CompletedTask;
        }
    }
}

namespace Scanner.MaUI.Services
{
    public static class FileSystem
    {
        public static string CacheDirectory => Path.GetTempPath();
    }
    public sealed class FileResult
    {
        public string FileName { get; init; }
        public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(File.OpenRead(FileName));
    }
}
