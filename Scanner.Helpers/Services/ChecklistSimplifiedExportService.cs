using Scanner.Models;

namespace Scanner.Helpers.Services
{
    public sealed class ChecklistSimplifiedExportResult
    {
        public ChecklistSimplifiedExportResult(IList<string> lines, int matchedCount)
        {
            Lines = lines;
            MatchedCount = matchedCount;
        }

        public IList<string> Lines { get; private set; }
        public int MatchedCount { get; private set; }
        public int MissingCount { get { return Lines.Count - MatchedCount; } }
    }

    public static class ChecklistSimplifiedExportService
    {
        public static ChecklistSimplifiedExportResult Build(IEnumerable<string> serialNumbers, IEnumerable<InboundChecklistRecord> records)
        {
            Dictionary<string, InboundChecklistRecord> recordBySn = (records ?? Enumerable.Empty<InboundChecklistRecord>())
                .Where(record => record != null && !string.IsNullOrWhiteSpace(record.Sn))
                .GroupBy(record => record.Sn.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var lines = new List<string>();
            int matchedCount = 0;
            foreach (string value in serialNumbers ?? Enumerable.Empty<string>())
            {
                string sn = (value ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(sn)) continue;
                InboundChecklistRecord record;
                if (!recordBySn.TryGetValue(sn, out record))
                {
                    lines.Add(Clean(sn) + " 不存在");
                    continue;
                }
                matchedCount++;
                bool repair = (record.Type ?? string.Empty).IndexOf("维修", StringComparison.OrdinalIgnoreCase) >= 0;
                lines.Add(Clean(sn) + " " + (repair ? "○" : "✔"));
            }
            return new ChecklistSimplifiedExportResult(lines, matchedCount);
        }

        private static string Clean(string value)
        {
            return (value ?? string.Empty).Replace("\t", " ").Replace("\r", " ").Replace("\n", " ").Trim();
        }
    }
}
