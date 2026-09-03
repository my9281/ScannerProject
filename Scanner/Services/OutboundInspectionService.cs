using Scanner.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Scanner.Services
{
    public static class OutboundInspectionService
    {
        public static IList<OutboundInspectionRecord> Build(string textPath, string baseWorkbookPath)
        {
            List<string> lines = File.ReadAllLines(textPath)
                .Select(line => (line ?? string.Empty).Trim().TrimStart('\uFEFF'))
                .Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            if (lines.Count == 0) throw new InvalidDataException("TXT 文件中没有有效数据。");
            if (lines.Count % 2 != 0) throw new InvalidDataException(string.Format("TXT 有 {0} 个非空行，必须严格按两行一条：第一行 SKU，第二行 SN。", lines.Count));
            IDictionary<string, InboundChecklistRecord> baseBySn = ChecklistXlsxReader.Read(baseWorkbookPath)
                .Where(item => !string.IsNullOrWhiteSpace(item.Sn))
                .GroupBy(item => item.Sn.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            List<OutboundInspectionRecord> result = new List<OutboundInspectionRecord>();
            for (int index = 0; index < lines.Count; index += 2)
            {
                string sku = lines[index];
                string sn = lines[index + 1];
                InboundChecklistRecord matched;
                bool found = baseBySn.TryGetValue(sn, out matched);
                result.Add(new OutboundInspectionRecord
                {
                    Number = result.Count + 1,
                    Sn = sn,
                    Sku = sku,
                    Type = found ? matched.Type : string.Empty,
                    ProcessingMethod = found ? matched.DetectionStatus : "不存在",
                    ProcessingTime = found ? matched.ProcessingTime : string.Empty,
                    IsMatched = found
                });
            }
            return result;
        }
    }
}
