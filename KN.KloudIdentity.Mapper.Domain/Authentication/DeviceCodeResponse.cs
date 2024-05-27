using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KN.KloudIdentity.Mapper.Domain.Authentication
{
    public class DeviceCodeResponse
    {
        [JsonProperty("device_code")]
        public string DeviceCode { get; set; }

        [JsonProperty("user_code")]
        public string UserCode { get; set; }

        [JsonProperty("verification_uri")]
        public string VerificationUri { get; set; }

        [JsonProperty("verification_uri_complete")]
        public string VerificationUriComplete { get; set; }

        [JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }

        [JsonProperty("interval")]
        public int Interval { get; set; }
    }

}
