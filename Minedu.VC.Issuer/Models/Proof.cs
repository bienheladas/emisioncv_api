namespace Minedu.VC.Issuer.Models
{
    public class Proof
    {
        public string Type { get; set; } = "JsonWebSignature2020";
        public string? Created { get; set; }
        public string ProofPurpose { get; set; } = "assertionMethod";
        public string? VerificationMethod { get; set; }
        public string? Jws { get; set; }
        public Anchor? Anchor { get; set; }
    }
}
