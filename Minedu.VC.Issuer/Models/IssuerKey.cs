namespace Minedu.VC.Issuer.Models
{
    public class IssuerKey
    {
        public string Kty { get; set; } = string.Empty;
        public string Crv { get; set; } = string.Empty;
        public string X { get; set; } = string.Empty;
        public string PrivateKeyMultibase { get; set; } = string.Empty;
    }
}
