using Microsoft.AspNetCore.Mvc;

namespace Minedu.VC.Issuer.Controllers
{
    [Route("v1/mimoto")]
    [ApiController]
    public class MimotoController : ControllerBase
    {
        private readonly IConfiguration _cfg;

        public MimotoController(IConfiguration cfg)
        {
            _cfg = cfg;
        }

        // Lista de emisores — pantalla "Agregar credencial" de Inji
        [HttpGet("issuers")]
        public IActionResult GetIssuers()
        {
            var (issuerBase, logoUrl, credConfigId) = GetIssuerInfo();

            return Ok(new
            {
                response = new
                {
                    issuers = new[]
                    {
                        BuildIssuerEntry(issuerBase, logoUrl, credConfigId)
                    }
                }
            });
        }

        // Detalle de un emisor individual
        [HttpGet("issuers/{issuerId}")]
        public IActionResult GetIssuer(string issuerId)
        {
            var (issuerBase, logoUrl, credConfigId) = GetIssuerInfo();

            return Ok(new
            {
                response = BuildIssuerEntry(issuerBase, logoUrl, credConfigId)
            });
        }

        // Lista de verifiers confiables — pantalla de confirmación de VP sharing
        [HttpGet("verifiers")]
        public IActionResult GetVerifiers()
        {
            var verifierBase = _cfg["Verifier:BaseUrl"] ?? "https://verificacioncv.ninnstack.com";

            return Ok(new
            {
                response = new
                {
                    verifiers = new[]
                    {
                        new
                        {
                            verifier_id   = "verificadorcv",
                            client_id     = _cfg["Verifier:Did"] ?? "did:web:verificacioncv.ninnstack.com",
                            display       = new[]
                            {
                                new
                                {
                                    name        = "Ministerio de Educación",
                                    locale      = "es",
                                    description = "Sistema de verificación de credenciales educativas"
                                }
                            }
                        }
                    }
                }
            });
        }

        // Propiedades generales de la app — tiene fallback estático en Inji si falla
        [HttpGet("allProperties")]
        public IActionResult GetAllProperties()
        {
            var issuerBase = _cfg["Oidc4Vci:IssuerBaseUrl"]!.TrimEnd('/');

            return Ok(new
            {
                response = new
                {
                    credentialRegistry = issuerBase,
                    issuerDomainName   = new Uri(issuerBase).Host,
                    toggles            = new { }
                }
            });
        }

        // ─── helpers ────────────────────────────────────────────────────────────

        private (string issuerBase, string logoUrl, string credConfigId) GetIssuerInfo()
        {
            var issuerBase  = _cfg["Oidc4Vci:IssuerBaseUrl"]!.TrimEnd('/');
            var logoUrl     = $"{issuerBase}/assets/minedu-logo.png";
            var credConfigId = _cfg["Oidc4Vci:CredentialConfigurationId"] ?? "certificado-estudios-vc";
            return (issuerBase, logoUrl, credConfigId);
        }

        private object BuildIssuerEntry(string issuerBase, string logoUrl, string credConfigId) => new
        {
            issuer_id             = "emisorcv",
            credential_issuer     = "emisorcv",
            credential_issuer_host = issuerBase,
            protocol              = "OpenId4VCI",
            display            = new[]
            {
                new
                {
                    name        = "Ministerio de Educación",
                    locale      = "es",
                    logo        = new { url = logoUrl, alt_text = "Logo MINEDU" },
                    title       = "Certificado de Estudios",
                    description = "Descarga tu certificado oficial de estudios emitido por el MINEDU"
                }
            },
            credential_configurations_supported = new[]
            {
                new
                {
                    id     = credConfigId,
                    format = "ldp_vc",
                    display = new[]
                    {
                        new
                        {
                            name             = "Certificado de Estudios",
                            locale           = "es",
                            background_color = "#CC0000",
                            text_color       = "#FFFFFF",
                            logo             = new { url = logoUrl, alt_text = "Ministerio de Educación" }
                        }
                    }
                }
            }
        };
    }
}
