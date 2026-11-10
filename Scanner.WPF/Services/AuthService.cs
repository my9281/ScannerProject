using Newtonsoft.Json;
using Scanner.Models;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Scanner.WPF.Services
{
    public class AuthService
    {
        private const string BaseUrl = "https://repair-rms.vercel.app";
        private const string LoginPath = "/api/v1/auth/login";
        private static readonly HttpClient HttpClient = CreateHttpClient();
        private static HttpClient CreateHttpClient()
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(20);
            return client;
        }

        public async Task<AuthSession> LoginAsync(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username))
            {
                throw new ArgumentException(Scanner.WPF.Helpers.UiText.Get("WpfComplete236"));
            }
            if (string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException(Scanner.WPF.Helpers.UiText.Get("WpfComplete237"));
            }
            LoginRequest requestData = new LoginRequest();
            requestData.Username = username.Trim();
            requestData.Password = password;
            string requestJson = JsonConvert.SerializeObject(requestData);
            using (StringContent content = new StringContent(requestJson, Encoding.UTF8, "application/json"))
            {
                HttpResponseMessage response;
                try
                {
                    response = await HttpClient.PostAsync(LoginPath, content);
                }
                catch (TaskCanceledException)
                {
                    throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfComplete238"));
                }
                catch (HttpRequestException ex)
                {
                    throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfComplete239") + ex.Message);
                }
                string responseJson = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string errorMessage = TryReadErrorMessage(responseJson);
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorMessage) ? Scanner.WPF.Helpers.UiText.Get("WpfComplete240") : errorMessage);
                    }
                    throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfComplete241") + ((int)response.StatusCode) + "：" + (string.IsNullOrWhiteSpace(errorMessage) ? response.ReasonPhrase : errorMessage));
                }
                LoginResponse loginResponse;
                try
                {
                    loginResponse = JsonConvert.DeserializeObject<LoginResponse>(responseJson);
                }
                catch (JsonException)
                {
                    throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfComplete242"));
                }
                if (loginResponse == null || string.IsNullOrWhiteSpace(loginResponse.Token))
                {
                    throw new InvalidOperationException(Scanner.WPF.Helpers.UiText.Get("WpfComplete243"));
                }
                DateTime expiresAt = ParseExpiration(loginResponse.ExpiresAt);
                AuthSession session = new AuthSession();
                session.Token = loginResponse.Token;
                session.Operator = loginResponse.Operator;
                session.Role = loginResponse.Role;
                session.ExpiresAt = expiresAt;
                return session;
            }
        }

        private static DateTime ParseExpiration(string expiresAtText)
        {
            DateTime expiresAt;
            if (!string.IsNullOrWhiteSpace(expiresAtText) && DateTime.TryParse(expiresAtText, null, System.Globalization.DateTimeStyles.RoundtripKind, out expiresAt))
            {
                return expiresAt.ToUniversalTime();
            }
            return DateTime.UtcNow.AddHours(12);
        }

        private static string TryReadErrorMessage(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }
            try
            {
                dynamic result = JsonConvert.DeserializeObject(json);
                if (result == null)
                {
                    return null;
                }
                if (result.message != null)
                {
                    return result.message.ToString();
                }
                if (result.error != null)
                {
                    return result.error.ToString();
                }
            }
            catch
            {
            }
            if (json.Length > 300)
            {
                return json.Substring(0, 300);
            }
            return json;
        }
    }
}
