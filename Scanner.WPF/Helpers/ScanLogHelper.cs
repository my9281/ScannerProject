using Scanner.WPF.Services;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Scanner.WPF.Helpers
{
    public sealed class ScanLogHelper
    {
        public ScanLogHelper()
            : this(false)
        {
        }

        public ScanLogHelper(bool replacementMode)
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string folder = Path.Combine(documents, "SN Label Printer");
            ConfigFilePath = replacementMode ? Path.Combine(folder, "replacement-label.config") : string.Empty;
            string configured = replacementMode && File.Exists(ConfigFilePath)
                ? File.ReadAllText(ConfigFilePath, Encoding.UTF8).Trim()
                : Properties.Settings.Default.CurrentScanLogFileName;
            string fileName = NormalizeFileName(configured, replacementMode ? "replacement_labels.txt" : "scanned_codes.txt");
            FilePath = Path.Combine(folder, fileName);
            WorkbookPath = Path.ChangeExtension(FilePath, ".xlsx");
            EnsureExists();
            if (replacementMode && !File.Exists(ConfigFilePath))
            {
                File.WriteAllText(ConfigFilePath, fileName, new UTF8Encoding(false));
            }
        }

        public string FilePath { get; private set; }
        public string WorkbookPath { get; private set; }
        public string ConfigFilePath { get; }

        public void Append(string code)
        {
            EnsureExists();
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + code + Environment.NewLine;
            File.AppendAllText(FilePath, line, new UTF8Encoding(true));
        }

        public void AppendWorkbook(string code, bool isOid, string selectedModel)
        {
            EnsureExists();
            ScanWorkbookHelper.Append(WorkbookPath, isOid ? string.Empty : selectedModel,
                isOid ? string.Empty : code, isOid ? code : string.Empty);
        }

        public void CreateNewFiles()
        {
            string prefix = string.IsNullOrEmpty(ConfigFilePath) ? "scanned_codes_" : "replacement_labels_";
            string fileName = prefix + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt";
            FilePath = Path.Combine(Path.GetDirectoryName(FilePath), fileName);
            WorkbookPath = Path.ChangeExtension(FilePath, ".xlsx");
            EnsureExists();
            if (string.IsNullOrEmpty(ConfigFilePath))
            {
                Properties.Settings.Default.CurrentScanLogFileName = fileName;
                Properties.Settings.Default.Save();
            }
            else File.WriteAllText(ConfigFilePath, fileName, new UTF8Encoding(false));
        }

        public void Open()
        {
            EnsureExists();
            Process.Start(new ProcessStartInfo { FileName = FilePath, UseShellExecute = true });
        }

        public void OpenWorkbook()
        {
            EnsureExists();
            Process.Start(new ProcessStartInfo { FileName = WorkbookPath, UseShellExecute = true });
        }

        public void WriteError(Exception exception)
        {
            string errorPath = Path.Combine(Path.GetDirectoryName(FilePath), "error.log");
            Directory.CreateDirectory(Path.GetDirectoryName(errorPath));
            File.AppendAllText(errorPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine + exception + Environment.NewLine + Environment.NewLine, Encoding.UTF8);
        }

        private void EnsureExists()
        {
            string folder = Path.GetDirectoryName(FilePath);
            Directory.CreateDirectory(folder);
            if (!File.Exists(FilePath))
            {
                File.WriteAllText(FilePath, string.Empty, new UTF8Encoding(true));
            }
            ScanWorkbookHelper.EnsureExists(WorkbookPath);
        }

        private static string NormalizeFileName(string configured, string fallback)
        {
            string name = Path.GetFileName(configured ?? string.Empty);
            return string.IsNullOrWhiteSpace(name) || !name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                ? fallback
                : name;
        }
    }
}
