using System.Text.Json.Serialization;

namespace Minedu.VC.Issuer.Models
{
    public class BitstringStatusListCredential
    {
        [JsonPropertyName("@context")]
        public string[] Context { get; set; } = Array.Empty<string>();
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("type")]
        public string[] Type { get; set; } = Array.Empty<string>();
        [JsonPropertyName("issuer")]
        public string Issuer { get; set; } = string.Empty;
        [JsonPropertyName("issuanceDate")]
        public DateTime IssuanceDate { get; set; }
        [JsonPropertyName("credentialSubject")]
        public BitstringStatusListSubject CredentialSubject { get; set; } = null!;
        [JsonPropertyName("proof")]
        public Proof? Proof { get; set; }
    }

    public class BitstringStatusListSubject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
        [JsonPropertyName("type")]
        public string Type { get; set; } = "BitstringStatusList";
        [JsonPropertyName("statusPurpose")]
        public string StatusPurpose { get; set; } = "revocation";
        [JsonPropertyName("encodedList")]
        public string EncodedList { get; set; } = string.Empty;
    }
}
