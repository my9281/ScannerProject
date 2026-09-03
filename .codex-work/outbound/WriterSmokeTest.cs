using Scanner.Models;
using Scanner.Services;
using System.Collections.Generic;

public static class WriterSmokeTest
{
    public static void Main()
    {
        OutboundInspectionXlsxWriter.Write("writer-test.xlsx", new List<OutboundInspectionRecord>
        {
            new OutboundInspectionRecord { Number = 1, Sn = "TESTSN001", Sku = "P-TEST-SKU-001", Type = "调拨入库", ProcessingMethod = "检测通过", ProcessingTime = "2026-08-25 10:30:00", IsMatched = true }
        });
    }
}
