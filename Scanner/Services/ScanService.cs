using Scanner.Helpers;
using System;

namespace Scanner.Services
{
    public sealed class ScanService
    {
        private readonly ScanLogHelper _log;
        private int _scanCount;

        public ScanService() : this(new ScanLogHelper())
        {
        }

        internal ScanService(ScanLogHelper log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public int ScanCount => _scanCount;
        public string LogFilePath => _log.FilePath;

        public string Record(string input)
        {
            string code = (input ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("扫描内容不能为空。", nameof(input));
            }

            _log.Append(code);
            _scanCount++;
            return code;
        }

        public void OpenLog()
        {
            _log.Open();
        }

        public void WriteError(Exception exception)
        {
            _log.WriteError(exception);
        }
    }
}
