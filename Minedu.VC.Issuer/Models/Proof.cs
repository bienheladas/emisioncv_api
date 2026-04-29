using System.Text.Json.Serialization;

namespace Minedu.VC.Issuer.Models
{
    public class Proof
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = "JsonWebSignature2020";
        [JsonPropertyName("created")]
        public string? Created { get; set; }
        [JsonPropertyName("proofPurpose")]
        public string ProofPurpose { get; set; } = "assertionMethod";
        [JsonPropertyName("verificationMethod")]
        public string? VerificationMethod { get; set; }
        [JsonPropertyName("jws")]
        public string? Jws { get; set; }
        [JsonPropertyName("anchor")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public Anchor? Anchor { get; set; }
    }
}
