using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Scanner.DI;
using Scanner.Models;
using Scanner.Helpers.Services;

class Program
{
    static void Main()
    {
        using (var root = new ScannerDependencies())
        using (var second = new ScannerDependencies())
        using (var mauiStyle = new ServiceCollection().AddScannerSharedServices().BuildServiceProvider())
        {
            Assert(ReferenceEquals(root.BaseData, root.BaseData), "WPF singleton");
            Assert(ReferenceEquals(mauiStyle.GetRequiredService<ChecklistDataCache>(), mauiStyle.GetRequiredService<ChecklistDataCache>()), "MAUI singleton");
            Assert(!ReferenceEquals(root.BaseData, second.BaseData), "Independent containers");
            Assert(typeof(InboundChecklistRecord).Assembly.GetName().Name == "Scanner.Models", "Models assembly");
            Assert(typeof(OutboundInspectionService).Assembly.GetName().Name == "Scanner.Helpers", "Helpers assembly");
            string input = Path.GetTempFileName();
            try
            {
                File.WriteAllLines(input, new[] { "SKU-1", "SN001" });
                root.BaseData.ReplaceRecords(new List<InboundChecklistRecord> { new InboundChecklistRecord { Sn = "SN001", Type = "first" } }, "first.xlsx");
                Assert(OutboundInspectionService.Build(input, root.BaseData.Records)[0].Type == "first", "Shared matching");
                root.BaseData.ReplaceRecords(new List<InboundChecklistRecord> { new InboundChecklistRecord { Sn = "SN001", Type = "second" } }, "second.xlsx");
                try { root.BaseData.ImportBase(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".xlsx")); } catch (IOException) { }
                Assert(root.BaseData.BaseDataFile == "second.xlsx", "Failed import preserves source");
                Assert(OutboundInspectionService.Build(input, root.BaseData.Records)[0].Type == "second", "Replacement records");
                Assert(second.BaseData.Records.Count == 0, "No static state leak");
            }
            finally { File.Delete(input); }
        }
        Console.WriteLine("PASS: shared assemblies, DI singleton ownership, matching, replacement and failed import preservation.");
    }
    static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
}
