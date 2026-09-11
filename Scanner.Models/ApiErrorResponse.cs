using Newtonsoft.Json;

namespace Scanner.Models
{
    public class ApiErrorResponse
    {
        [JsonProperty("success")]
        public bool Success { get; set; }

        [JsonProperty("error")]
        public ApiErrorDetail Error { get; set; }
    }

    public class ApiErrorDetail
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }
}
