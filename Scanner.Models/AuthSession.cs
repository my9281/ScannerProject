using Newtonsoft.Json;

namespace Scanner.Models
{
    public class AuthSession
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("operator")]
        public string Operator { get; set; }

        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("expires_at")]
        public DateTime ExpiresAt { get; set; }

        [JsonIgnore]
        public bool IsLocalMode { get; set; }

        public bool IsValid()
        {
            if (IsLocalMode)
            {
                return true;
            }
            if (string.IsNullOrWhiteSpace(Token))
            {
                return false;
            }
            return DateTime.UtcNow < ExpiresAt.ToUniversalTime().AddMinutes(-1);
        }
    }
}
