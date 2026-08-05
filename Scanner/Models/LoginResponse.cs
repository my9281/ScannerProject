using Newtonsoft.Json;

namespace Scanner.Models
{
    public class LoginResponse
    {
        [JsonProperty("token")]
        public string Token
        {
            get;
            set;
        }

        [JsonProperty("operator")]
        public string Operator
        {
            get;
            set;
        }

        [JsonProperty("role")]
        public string Role
        {
            get;
            set;
        }

        [JsonProperty("expires_at")]
        public string ExpiresAt
        {
            get;
            set;
        }
    }
}