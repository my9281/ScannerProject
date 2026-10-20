using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace Scanner.WPF.Services
{
    public sealed class BluettiLoginConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string SiteId { get; set; }
    }

    public sealed class BluettiConfigStore
    {
        public string FilePath { get; }
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Scanner.Bluetti.Config.v1");
        public BluettiConfigStore(string filePath = null)
        {
            FilePath = filePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Scanner", "Bluetti", "bluetti.config");
        }

        public void Save(BluettiLoginConfig config)
        {
            if (string.IsNullOrWhiteSpace(config?.Username) || string.IsNullOrEmpty(config.Password))
                throw new ArgumentException("Account and password are required.");
            var encrypted = Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(config.Password),
                Entropy, DataProtectionScope.CurrentUser));
            var document = new XDocument(new XElement("configuration", new XElement("appSettings",
                new XElement("add", new XAttribute("key", "Username"), new XAttribute("value", config.Username.Trim())),
                new XElement("add", new XAttribute("key", "ProtectedPassword"), new XAttribute("value", encrypted)),
                new XElement("add", new XAttribute("key", "SiteId"), new XAttribute("value", config.SiteId ?? "")))));
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            document.Save(FilePath + ".tmp");
            File.Move(FilePath + ".tmp", FilePath, true);
        }

        public BluettiLoginConfig Load()
        {
            if (!File.Exists(FilePath)) return null;
            var settings = XDocument.Load(FilePath).Root?.Element("appSettings")?.Elements("add")
                .ToDictionary(e => (string)e.Attribute("key"), e => (string)e.Attribute("value"));
            if (settings == null || !settings.TryGetValue("Username", out var username) ||
                !settings.TryGetValue("ProtectedPassword", out var encrypted)) throw new InvalidDataException("Invalid BLUETTI config.");
            var password = Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(encrypted),
                Entropy, DataProtectionScope.CurrentUser));
            settings.TryGetValue("SiteId", out var siteId);
            return new BluettiLoginConfig { Username = username, Password = password, SiteId = siteId };
        }

        public void Clear()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }
}
