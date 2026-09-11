using System;
using System.Collections.Generic;
using System.IO;
using Scanner.Models;
using Scanner.Services;
class SharedBaseSmoke
{
    static void Main()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path, new[] { "SKU", "SN001" });
            ChecklistDataCache.ReplaceRecords(new List<InboundChecklistRecord> { new InboundChecklistRecord { Sn = "SN001", Type = "first" } }, "first.xlsx");
            if (OutboundInspectionService.Build(path, ChecklistDataCache.Records)[0].Type != "first") throw new Exception("Initial cache mismatch");
            ChecklistDataCache.ReplaceRecords(new List<InboundChecklistRecord> { new InboundChecklistRecord { Sn = "SN001", Type = "second" } }, "second.xlsx");
            try { ChecklistDataCache.ImportBase("missing-base-file.xlsx"); } catch (IOException) { }
            if (ChecklistDataCache.BaseDataFile != "second.xlsx" || OutboundInspectionService.Build(path, ChecklistDataCache.Records)[0].Type != "second") throw new Exception("Replacement/rollback mismatch");
            Console.WriteLine("PASS: shared records, replacement, failed import preserves cache");
        }
        finally { File.Delete(path); }
    }
}
