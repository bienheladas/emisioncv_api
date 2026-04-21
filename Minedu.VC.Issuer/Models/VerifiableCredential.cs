using System;
using System.Text.Json.Serialization;

namespace Minedu.VC.Issuer.Models
{
    public class VerifiableCredential
    {
        [JsonPropertyName("@context")]
        public string[] Context { get; set; } = new[]
        {
            "https://www.w3.org/2018/credentials/v1",
            "https://emisorcv.ninnstack.com/vc/context.jsonld",
            "https://w3id.org/security/jws/v1"
        };

        [JsonPropertyName("id")]
        public string Id { get; set; } = $"urn:uuid:{Guid.NewGuid()}";

        [JsonPropertyName("type")]
        public string[] Type { get; set; } = { "VerifiableCredential", "CertificadoEstudios" };

        [JsonPropertyName("issuer")]
        public string Issuer { get; set; } = "did:web:emisorcv.ninnstack.com";

        [JsonPropertyName("issuanceDate")]
        public DateTime IssuanceDate { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("credentialSubject")]
        public CredentialSubject CredentialSubject { get; set; }

        // Optional extras we'll add in siguientes pasos:
        [JsonPropertyName("credentialSchema")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CredentialSchema CredentialSchema { get; set; }

        [JsonPropertyName("credentialStatus")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public CredentialStatus CredentialStatus { get; set; }

        [JsonPropertyName("proof")]
        public Proof Proof { get; set; }
    }

    public class CredentialSchema
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; } // URL to your schema or context

        [JsonPropertyName("type")]
        public string? Type { get; set; } // e.g., "JsonSchemaValidator2018" or "JsonSchema"
    }

    public class CredentialStatus
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; } // URL to status entry (e.g., StatusList2021)

        [JsonPropertyName("type")]
        public string? Type { get; set; } // e.g., "StatusList2021Entry"

        [JsonPropertyName("statusPurpose")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? StatusPurpose { get; set; } // e.g., "revocation" or "suspension"

        [JsonPropertyName("statusListIndex")]
        public int? StatusListIndex { get; set; }

        [JsonPropertyName("statusListCredential")]
        public string? StatusListCredential { get; set; }
    }
}
