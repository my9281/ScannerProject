using Scanner.Helpers;
using System;
using System.Collections.Generic;

namespace Scanner.Services
{
    public sealed class ScanService
    {
        private readonly ScanLogHelper _log;
        private readonly OidService _oid;
        private readonly HashSet<string> _recordedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private int _scanCount;
        public ScanService() : this(new ScanLogHelper(), new OidService())
        {
        }

        internal ScanService(ScanLogHelper log, OidService oid)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
            _oid = oid ?? throw new ArgumentNullException(nameof(oid));
        }

        public int ScanCount => _scanCount;
        public string LogFilePath => _log.FilePath;

        public ScanResult Record(string input)
        {
            string code = (input ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new ArgumentException("扫描内容不能为空。", nameof(input));
            }
            bool wasRecorded = _recordedCodes.Add(code);
            if (wasRecorded)
            {
                _log.Append(code);
                _scanCount++;
            }
            return new ScanResult(code, wasRecorded, _oid.Inspect(code));
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

    public sealed class ScanResult
    {
        public ScanResult(string code, bool wasRecorded, OidScanResult oid)
        {
            Code = code;
            WasRecorded = wasRecorded;
            Oid = oid ?? throw new ArgumentNullException(nameof(oid));
        }

        public string Code { get; }
        public bool WasRecorded { get; }
        public OidScanResult Oid { get; }
    }
}
