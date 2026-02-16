using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TigerStockLevelManager.Models
{
    internal class LogoToken
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; }

        [JsonPropertyName("token_type")]
        public string TokenType { get; set; }

        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; }

        [JsonPropertyName("as:client_id")]
        public string ClientId { get; set; }

        [JsonPropertyName("userName")]
        public string UserName { get; set; }

        [JsonPropertyName("firmNo")]
        public string FirmNo { get; set; }

        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; }

        [JsonPropertyName("dbName")]
        public string DbName { get; set; }

        [JsonPropertyName("logoDB")]
        public string LogoDB { get; set; }

        [JsonPropertyName("isLoginEx")]
        public string IsLoginEx { get; set; }

        [JsonPropertyName("isLogoPlugin")]
        public string IsLogoPlugin { get; set; }

        [JsonPropertyName("useIdm")]
        public string UseIdm { get; set; }

        [JsonPropertyName(".issued")]
        public string Issued { get; set; }

        [JsonPropertyName(".expires")]
        public string Expires { get; set; }
    }
}
