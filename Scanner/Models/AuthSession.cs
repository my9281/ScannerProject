using Newtonsoft.Json;
using System;

namespace Scanner.Models
{
    public class AuthSession
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
        public DateTime ExpiresAt
        {
            get;
            set;
        }

        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(Token))
            {
                return false;
            }

            /*
             * 提前一分钟视为过期，
             * 避免刚进入主界面 Token 就失效。
             */
            return DateTime.UtcNow <
                   ExpiresAt.ToUniversalTime()
                       .AddMinutes(-1);
        }
    }
}