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
        public string FilePath => "in-memory";
        public string WorkbookPath => "in-memory.xlsx";
        public void Append(string code) { }
        public void AppendWorkbook(string code, bool isOid, string selectedModel) { }
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
        public Task AppendAsync(string code) => Task.CompletedTask;
    }
}
