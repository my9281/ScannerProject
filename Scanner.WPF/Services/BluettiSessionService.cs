using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Scanner.WPF.Services
{
    public sealed class BluettiSession
    {
        public string Token { get; set; }
        public string Uid { get; set; }
        public string DisplayName { get; set; }
        public string SiteId { get; set; }
        public string SiteName { get; set; }
    }

    public sealed class BluettiSite
    {
        public string SiteId { get; set; }
        public string SiteName { get; set; }
    }

    public sealed class BluettiAuthenticationException : Exception
    {
        public BluettiAuthenticationException() : base("BLUETTI authentication expired or rejected.") { }
    }

    // Separate from Scanner's own authentication. Never persists the password.
    public sealed class BluettiSessionService
    {
        private static readonly HttpClient Client = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false
        }) { Timeout = TimeSpan.FromSeconds(30) };
        private readonly HttpClient _client;
        private readonly string _file;
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Scanner.Bluetti.Session.v1");

        public BluettiSessionService(HttpClient client = null, string sessionFile = null)
        {
            _client = client ?? Client;
            _file = sessionFile ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Scanner", "Bluetti", "session.dat");
        }

        public async Task<BluettiSession> LoginAsync(string username, string password, CancellationToken cancellationToken)
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(username.Trim()), "username");
            form.Add(new StringContent(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(password)))), "password");
            var data = await SendAsync(HttpMethod.Post, "/accessToken", null, form, cancellationToken);
            var token = (string)data?["token"];
            if (string.IsNullOrWhiteSpace(token)) throw new InvalidDataException("Missing login token.");
            var session = new BluettiSession { Token = token };
            await ValidateAsync(session, cancellationToken);
            return session;
        }

        public async Task ValidateAsync(BluettiSession session, CancellationToken cancellationToken)
        {
            var data = await SendAsync(HttpMethod.Get, "/api/userInfo", session, null, cancellationToken);
            session.Uid = (string)data?["uid"];
            session.DisplayName = (string)data?["nickname"] ?? session.Uid;
            if (string.IsNullOrWhiteSpace(session.Uid)) throw new InvalidDataException("Missing user information.");
        }

        public async Task<List<BluettiSite>> GetSitesAsync(BluettiSession session, CancellationToken cancellationToken)
        {
            var data = await SendAsync(HttpMethod.Get, "/api/bluas/client/user/v1/getUserSite?uid=" +
                Uri.EscapeDataString(session.Uid), session, null, cancellationToken);
            if (!(data is JArray sites)) throw new InvalidDataException("Invalid site list.");
            return sites.Where(s => (int?)s["enable"] != 0 && !string.IsNullOrWhiteSpace((string)s["siteId"]))
                .Select(s => new BluettiSite { SiteId = (string)s["siteId"], SiteName = (string)s["siteName"] }).ToList();
        }

        public async Task SelectSiteAsync(BluettiSession session, BluettiSite site, CancellationToken cancellationToken)
        {
            var candidate = new BluettiSession { Token = session.Token, SiteId = site.SiteId };
            await SendAsync(HttpMethod.Get, "/api/bluas/client/role/v1/existPermissions", candidate, null, cancellationToken);
            session.SiteId = site.SiteId;
            session.SiteName = site.SiteName;
        }

        private async Task<JToken> SendAsync(HttpMethod method, string path, BluettiSession session,
            HttpContent content, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(method, "https://gwpry.bluettipower.com" + path) { Content = content };
            request.Headers.Add("x-app-key", "18880A4BA6CF791157564C2ECD0");
            request.Headers.Add("x-os", "web");
            request.Headers.Add("Accept-Language", "zh-CN");
            if (session != null)
            {
                request.Headers.Add("Authorization", session.Token);
                if (!string.IsNullOrWhiteSpace(session.SiteId)) request.Headers.Add("x-app-resource", session.SiteId);
            }
            using var response = await _client.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                throw new BluettiAuthenticationException();
            response.EnsureSuccessStatusCode();
            var json = JObject.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            int? code = (int?)json["msgCode"];
            if (code >= 800 && code <= 999) throw new BluettiAuthenticationException();
            if (code != 0) throw new InvalidDataException("BLUETTI response code: " + code);
            return json["data"];
        }

        public void Save(BluettiSession session)
        {
            if (string.IsNullOrWhiteSpace(session?.Token) || string.IsNullOrWhiteSpace(session.SiteId))
                throw new InvalidOperationException("Login and site selection are required.");
            Directory.CreateDirectory(Path.GetDirectoryName(_file));
            var bytes = ProtectedData.Protect(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(session)),
                Entropy, DataProtectionScope.CurrentUser);
            var temporary = _file + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, _file, true);
        }

        public BluettiSession Load()
        {
            if (!File.Exists(_file)) return null;
            try
            {
                var bytes = ProtectedData.Unprotect(File.ReadAllBytes(_file), Entropy, DataProtectionScope.CurrentUser);
                var session = JsonConvert.DeserializeObject<BluettiSession>(Encoding.UTF8.GetString(bytes));
                if (!string.IsNullOrWhiteSpace(session?.Token) && !string.IsNullOrWhiteSpace(session.SiteId)) return session;
            }
            catch (CryptographicException) { }
            catch (JsonException) { }
            Clear();
            return null;
        }

        public void Clear()
        {
            if (File.Exists(_file)) File.Delete(_file);
        }
    }
}
