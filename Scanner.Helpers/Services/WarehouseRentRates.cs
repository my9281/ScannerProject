using System.Text.RegularExpressions;

namespace Scanner.Helpers.Services
{
    public static class WarehouseRentRates
    {
        private static readonly IReadOnlyDictionary<string, decimal> Rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["AC180"] = 0.08m, ["AC180UN"] = 0.08m, ["AC180US"] = 0.08m,
            ["AC200L"] = 0.12m, ["AC200MAX"] = 0.12m, ["AC200PL"] = 0.12m,
            ["AC240"] = 0.12m, ["AC240US"] = 0.12m, ["AC2A"] = 0.02m,
            ["AC300"] = 0.16m, ["AC500"] = 0.24m, ["AC50B"] = 0.04m,
            ["AC60"] = 0.04m, ["AC60US"] = 0.04m, ["AC70"] = 0.04m,
            ["AP300"] = 0.16m, ["APEX300"] = 0.16m, ["B1232"] = 0.08m,
            ["B300"] = 0.16m, ["B300K"] = 0.16m, ["B300K2"] = 0.16m,
            ["B300S"] = 0.16m, ["B4810"] = 0.06m, ["BLC200"] = 0.06m,
            ["CHARGER1"] = 0.01m, ["CHARGER2"] = 0.02m, ["DISPLAY1"] = 0.01m,
            ["EB3A"] = 0.02m, ["EB70"] = 0.04m, ["EB70S"] = 0.04m,
            ["EL10"] = 0.01m, ["EL100M"] = 0.04m, ["EL100V2"] = 0.04m,
            ["EL200V2"] = 0.08m, ["EL300"] = 0.12m, ["EL30V2"] = 0.02m,
            ["EL400"] = 0.16m, ["ELITE200"] = 0.08m, ["ELITE200V2"] = 0.08m,
            ["EP500US"] = 1m, ["F045D"] = 0.16m, ["FRPOWER"] = 0.06m,
            ["HANDSFREE1"] = 0.06m, ["HD1"] = 0.06m, ["HF1"] = 0.06m,
            ["HF2"] = 0.06m, ["HUBD1"] = 0.01m, ["PIONEERNA"] = 0.08m,
            ["PS54"] = 0.06m, ["PV100"] = 0m, ["PV100D"] = 0m,
            ["PV100FX"] = 0m, ["PV200"] = 0m, ["PV350"] = 0m,
            ["PV350D"] = 0m, ["RV5"] = 0.06m, ["SORA60"] = 0m,
            ["SP100L"] = 0m, ["X30"] = 0.02m, ["X60"] = 0.04m
        };

        public static bool TryGet(string skuOrModel, out decimal rate)
        {
            string key = Regex.Replace((skuOrModel ?? "").ToUpperInvariant(), @"\s+", "");
            if (Rates.TryGetValue(key, out rate)) return true;
            // Full product SKU: P-<model>-<region>-... . Do not use prefix matching (e.g. AC180P is not AC180).
            var parts = key.Split('-');
            if (parts.Length >= 3 && parts[0] == "P")
            {
                if (Rates.TryGetValue(parts[1] + parts[2], out rate)) return true;
                if (Rates.TryGetValue(parts[1], out rate)) return true;
            }
            rate = 0;
            return false;
        }
    }
}
