using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Scanner.Services
{
    public sealed class MeterModelService
    {
        private static readonly string[] BuiltInModels =
        {
            "AC200PL", "AC200L", "AC200P", "AC200M", "AC180P", "AC180T",
            "EL100V2", "EL200V2", "B300K2", "SP100L", "AC180", "AC240",
            "AC300", "AC500", "EL30V2", "EL300", "EL400", "B300K",
            "B300S", "B500K", "PV350", "PV200", "AP300", "AC50B",
            "AC2A", "AC2P", "EB3A", "AC60", "AC70", "EL10", "B230",
            "B300", "PS54", "EB55", "EB70", "PINA"
        };
        private readonly List<string> _customModels;

        public MeterModelService() : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YM-Star Scanner",
            "custom-models.config"))
        {
        }

        internal MeterModelService(string customModelsFilePath)
        {
            if (string.IsNullOrWhiteSpace(customModelsFilePath))
            {
                throw new ArgumentException("自定义型号配置文件路径不能为空。", nameof(customModelsFilePath));
            }
            CustomModelsFilePath = customModelsFilePath;
            _customModels = LoadCustomModels();
        }

        public string CustomModelsFilePath { get; }

        public IReadOnlyList<string> GetModels()
        {
            return BuiltInModels
                .Concat(_customModels)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(model => model, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public string FindModel(string scannedCode)
        {
            if (string.IsNullOrWhiteSpace(scannedCode))
            {
                return string.Empty;
            }
            foreach (string model in GetModels().OrderByDescending(value => value.Length))
            {
                if (scannedCode.IndexOf(model, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return model;
                }
            }
            return string.Empty;
        }

        public string AddCustomModel(string model)
        {
            string normalized = NormalizeModel(model);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                throw new ArgumentException("型号不能为空。", nameof(model));
            }
            string existing = GetModels().FirstOrDefault(value => string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }
            _customModels.Add(normalized);
            try
            {
                SaveCustomModels();
            }
            catch
            {
                _customModels.Remove(normalized);
                throw;
            }
            return normalized;
        }

        private List<string> LoadCustomModels()
        {
            if (!File.Exists(CustomModelsFilePath))
            {
                return new List<string>();
            }
            try
            {
                return File.ReadAllLines(CustomModelsFilePath, Encoding.UTF8)
                    .Select(NormalizeModel)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                return new List<string>();
            }
        }

        private void SaveCustomModels()
        {
            string folder = Path.GetDirectoryName(CustomModelsFilePath);
            if (!string.IsNullOrWhiteSpace(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllLines(CustomModelsFilePath, _customModels.OrderBy(value => value, StringComparer.OrdinalIgnoreCase), new UTF8Encoding(true));
        }

        private static string NormalizeModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model))
            {
                return string.Empty;
            }
            return string.Concat(model.Where(character => !char.IsWhiteSpace(character))).ToUpperInvariant();
        }
    }
}
