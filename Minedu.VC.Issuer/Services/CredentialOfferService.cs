using Minedu.VC.Issuer.Models;
using Minedu.VC.Issuer.Models.Dto;
using Minedu.VC.Issuer.Services.Auth;

namespace Minedu.VC.Issuer.Services
{
    public class CredentialOfferService
    {
        private readonly AuthorizationService _auth;
        private readonly IConfiguration _config;

        public CredentialOfferService(AuthorizationService auth, IConfiguration config)
        {
            _auth = auth;
            _config = config;
        }

        public (string code, CredentialOffer offer, string offerUri) CreateOfferForSolicitud(int idSolicitud)
        {
            var issuerBase = _config["Oidc4Vci:IssuerBaseUrl"]
                             ?? throw new InvalidOperationException("Missing Oidc4Vci:IssuerBaseUrl");
            var issuerIdentifier = _config["Oidc4Vci:IssuerIdentifier"]!;
            var credConfigId = _config["Oidc4Vci:CredentialConfigurationId"] ?? "certificado-estudios-vc";

            var code = _auth.GeneratePreAuthorizedCode(idSolicitud, TimeSpan.FromMinutes(10));

            var offer = new CredentialOffer
            {
                credential_issuer = issuerIdentifier,
                credential_configuration_ids = new[] { credConfigId },
                grants = new CredentialOffer.Grants
                {
                    pre_authorized_code = new CredentialOffer.PreAuthorizedCodeGrant
                    {
                        pre_authorized_code = code
                    }
                }
            };

            var offerUri = $"{issuerBase.TrimEnd('/')}/offer/{code}";
            return (code, offer, offerUri);
        }
    }
}
