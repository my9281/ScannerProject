using Scanner.Models;
using Scanner.Helpers.Services;
using System;
using System.Collections.Generic;
using System.Linq;

internal static class Program
{
    private static int Main()
    {
        try
        {
            var records = new List<InboundChecklistRecord>
            {
                new InboundChecklistRecord { Sn = "EL100V22532031108646", Type = "退货退款", DetectionStatus = "待检测", ProcessingTime = "2026-09-03 15:59:29" },
                new InboundChecklistRecord { Sn = "REPAIR-1", Type = "退回维修" },
                new InboundChecklistRecord { Sn = "REPAIR-2", Type = "其他维修项目" }
            };
            ChecklistSimplifiedExportResult result = ChecklistSimplifiedExportService.Build(
                new[] { "EL100V22532031108646", "repair-1", "REPAIR-2", "MISSING-1" }, records);
            string[] expected =
            {
                "EL100V22532031108646 ✔",
                "repair-1 ○",
                "REPAIR-2 ○",
                "MISSING-1 不存在"
            };
            Assert(result.Lines.SequenceEqual(expected), "Simplified lines are incorrect");
            Assert(result.MatchedCount == 3, "Matched count is incorrect");
            Assert(result.MissingCount == 1, "Missing count is incorrect");
            Console.WriteLine("PASS: non-repair check mark, repair circle, case-insensitive SN match and missing SN output.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return 1;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
