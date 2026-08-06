using System;

namespace Scanner.Services
{
    public sealed class MeterModelService
    {
        private static readonly string[] Models =
        {
            "AC200PL", "AC200L", "AC200P", "AC200M", "AC180P", "AC180T",
            "EL100V2", "EL200V2", "B300K2", "SP100L", "AC180", "AC240",
            "AC300", "AC500", "EL30V2", "EL300", "EL400", "B300K",
            "B300S", "B500K", "PV350", "PV200", "AP300", "AC50B",
            "AC2A", "AC2P", "EB3A", "AC60", "AC70", "EL10", "B230",
            "B300", "PS54", "EB55", "EB70", "PINA"
        };

        public string FindModel(string scannedCode)
        {
            if (string.IsNullOrWhiteSpace(scannedCode))
            {
                return string.Empty;
            }
            foreach (string model in Models)
            {
                if (scannedCode.IndexOf(model, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return model;
                }
            }
            return string.Empty;
        }
    }
}
