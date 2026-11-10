using System;
using System.IO;

namespace Scanner.WPF.Services
{
    public static class BluettiDiagnostics
    {
        public static void Write(string message)
        {
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scanner", "Bluetti");
                Directory.CreateDirectory(folder);
                var file = Path.Combine(folder, "sync.log");
                if (File.Exists(file) && new FileInfo(file).Length > 262144) File.WriteAllText(file, "");
                File.AppendAllText(file, DateTimeOffset.Now.ToString("O") + " " + message + Environment.NewLine);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
