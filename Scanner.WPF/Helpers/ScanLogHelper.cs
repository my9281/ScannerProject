using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Scanner.WPF.Services;

namespace Scanner.WPF.Helpers
{
    public sealed class ScanLogHelper
    {
        public ScanLogHelper()
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string folder = Path.Combine(documents, "SN Label Printer");
            string fileName = NormalizeFileName(Properties.Settings.Default.CurrentScanLogFileName);
            FilePath = Path.Combine(folder, fileName);
            WorkbookPath = Path.ChangeExtension(FilePath, ".xlsx");
            EnsureExists();
        }

        public string FilePath { get; private set; }
        public string WorkbookPath { get; private set; }

        public void Append(string code)
        {
            EnsureExists();
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + code + Environment.NewLine;
            File.AppendAllText(FilePath, line, new UTF8Encoding(true));
            bool isOid = OidService.IsOid(code);
            string model = isOid ? string.Empty : new MeterModelService().FindModel(code);
            ScanWorkbookHelper.Append(WorkbookPath, model, isOid ? string.Empty : code, isOid ? code : string.Empty);
        }

        public void CreateNewFiles()
        {
            string fileName = "scanned_codes_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".txt";
            FilePath = Path.Combine(Path.GetDirectoryName(FilePath), fileName);
            WorkbookPath = Path.ChangeExtension(FilePath, ".xlsx");
            EnsureExists();
            Properties.Settings.Default.CurrentScanLogFileName = fileName;
            Properties.Settings.Default.Save();
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

        private static string NormalizeFileName(string configured)
        {
            string name = Path.GetFileName(configured ?? string.Empty);
            return string.IsNullOrWhiteSpace(name) || !name.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)
                ? "scanned_codes.txt"
                : name;
        }
    }
}
