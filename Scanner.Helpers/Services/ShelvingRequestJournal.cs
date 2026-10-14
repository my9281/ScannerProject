using System.Security.Cryptography;
using System.Text;
namespace Scanner.Helpers.Services
{
    internal static class ShelvingRequestJournal
    {
        private static readonly object Sync = new object();
        public static string PathFor(string server, string content)
        {
            string hash;
            using (var sha = SHA256.Create())
                hash = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(server + "\n" + content))).Replace("-", "");
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Scanner", "shelving-requests");
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, hash + ".request");
        }
        public static Guid GetOrCreate(string path)
        {
            lock (Sync)
            {
                if (File.Exists(path))
                {
                    if (Guid.TryParse(File.ReadAllText(path), out Guid existing) && existing != Guid.Empty) return existing;
                    throw new InvalidOperationException("上架请求记录损坏，请核对服务端是否已入库后再处理。");
                }
                Guid id = Guid.NewGuid();
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream)) writer.Write(id.ToString("D"));
                return id;
            }
        }
        public static void Complete(string path)
        {
            lock (Sync) { if (File.Exists(path)) File.Delete(path); }
        }
    }
}
