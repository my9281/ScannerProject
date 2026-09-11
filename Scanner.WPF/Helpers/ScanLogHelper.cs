using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Scanner.WPF.Helpers
{
    public sealed class ScanLogHelper
    {
        public ScanLogHelper()
        {
            string documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            FilePath = Path.Combine(documents, "SN Label Printer", "scanned_codes.txt");
        }

        public string FilePath { get; }

        public void Append(string code)
        {
            EnsureExists();
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\t" + code + Environment.NewLine;
            File.AppendAllText(FilePath, line, new UTF8Encoding(true));
        }

        public void Open()
        {
            EnsureExists();
            Process.Start(new ProcessStartInfo { FileName = FilePath, UseShellExecute = true });
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
        }
    }
}
