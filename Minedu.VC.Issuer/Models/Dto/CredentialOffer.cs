using System.Text.Json.Serialization;

namespace Minedu.VC.Issuer.Models.Dto
{
    public class CredentialOffer
    {
        public string credential_issuer { get; set; } = default!;
        public string[] credential_configuration_ids { get; set; } = default!;
        public Grants grants { get; set; } = new();

        public class Grants
        {
            [JsonPropertyName("urn:ietf:params:oauth:grant-type:pre-authorized_code")]
            public PreAuthorizedCodeGrant? pre_authorized_code { get; set; }

            // Si más adelante soportas authorization_code:
            // [JsonPropertyName("authorization_code")]
            // public AuthorizationCodeGrant? authorization_code { get; set; }
        }

        public class PreAuthorizedCodeGrant
        {
            [JsonPropertyName("pre-authorized_code")]
            public string pre_authorized_code { get; set; } = default!;
        }
    }
}
