using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Scanner.Services
{
    public sealed class OidService : IDisposable
    {
        private static readonly Regex LongNumericOid = new Regex(@"^\d{34}$", RegexOptions.Compiled);
        private static readonly Regex FedExOid = new Regex(@"^\d{12}$", RegexOptions.Compiled);
        private static readonly Regex AmazonOid = new Regex(@"^1Z[A-Z0-9]{16}$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly Dictionary<string, int> _scanCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private readonly SpeechService _speech;
        public OidService()
        {
            _speech = new SpeechService();
        }
        public OidScanResult Inspect(string code)
        {
            string normalized = (code ?? string.Empty).Trim();
            if (!IsOid(normalized))
            {
                return OidScanResult.NotOid;
            }
            int count;
            _scanCounts.TryGetValue(normalized, out count);
            count++;
            _scanCounts[normalized] = count;
            SpeakOid();
            return new OidScanResult(true, count);
        }

        public static bool IsOid(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }
            string normalized = code.Trim();
            return LongNumericOid.IsMatch(normalized) || FedExOid.IsMatch(normalized) || AmazonOid.IsMatch(normalized);
        }

        public void Dispose()
        {
            _speech.Dispose();
        }

        private void SpeakOid()
        {
            _speech.SpeakOid();
        }
    }

    public sealed class OidScanResult
    {
        public static readonly OidScanResult NotOid = new OidScanResult(false, 0);
        public OidScanResult(bool isOid, int occurrence)
        {
            IsOid = isOid;
            Occurrence = occurrence;
        }

        public bool IsOid { get; }
        public int Occurrence { get; }
        public bool ShouldPrint => IsOid && Occurrence >= 2;
    }
}
