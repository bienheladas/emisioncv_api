using Microsoft.AspNetCore.Mvc;

namespace Minedu.VC.Issuer.Controllers
{
    [Route(".well-known")]
    [ApiController]
    public class WellKnownController : ControllerBase
    {
        private readonly ILogger<WellKnownController> _logger;

        public WellKnownController(ILogger<WellKnownController> logger)
        {
            _logger = logger;
        }

        [HttpGet("openid-credential-issuer")]
        public IActionResult IssuerMetadata([FromServices] IConfiguration cfg)
        {
            var callerIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers["User-Agent"].ToString();
            var accept = Request.Headers["Accept"].ToString();

            _logger.LogInformation(
                """
                🔍 METADATA REQUEST RECEIVED
                From IP: {Ip}
                User-Agent: {UA}
                Accept: {Accept}
                """,
                callerIp, ua, accept
            );

            try
            {
                var issuerBase = cfg["Oidc4Vci:IssuerBaseUrl"]!;
                var issuerIdentifier = cfg["Oidc4Vci:IssuerIdentifier"]!;
                var credConfigId = cfg["Oidc4Vci:CredentialConfigurationId"] ?? "certificado-estudios-vc";

                var assetsBase = $"{issuerBase.TrimEnd('/')}/assets";
                var logoUrl = $"{assetsBase}/minedu-logo.png";

                var metadata = new
                {
                    credential_issuer = issuerIdentifier,
                    authorization_servers = new[] { issuerIdentifier },
                    credential_endpoint = $"{issuerBase.TrimEnd('/')}/issuer/credential",
                    display = new[]
                    {
                        new
                        {
                            name = "Ministerio de Educación",
                            locale = "es",
                            logo = new { url = logoUrl, alt_text = "Logo Ministerio de Educación del Perú" }
                        }
                    },
                    credential_configurations_supported = new Dictionary<string, object>
                    {
                        [credConfigId] = new
                        {
                            format = "ldp_vc",
                            display = new[]
                            {
                                new
                                {
                                    name = "Certificado de Estudios",
                                    locale = "es",
                                    background_color = "#CC0000",
                                    text_color = "#FFFFFF",
                                    logo = new { url = logoUrl, alt_text = "Ministerio de Educación" }
                                }
                            },
                            credential_definition = new
                            {
                                type = new[] { "VerifiableCredential", "CertificadoEstudios" },
                                credentialSubject = new Dictionary<string, object>
                                {
                                    ["titular"] = new { display = new[] { new { name = "Titular", locale = "es" } } },
                                    ["modalidad"] = new { display = new[] { new { name = "Modalidad", locale = "es" } } },
                                    ["nivel"] = new { display = new[] { new { name = "Nivel", locale = "es" } } },
                                    ["gradosConcluidos"] = new { display = new[] { new { name = "Grados Concluidos", locale = "es" } } },
                                    ["institucionEducativa"] = new { display = new[] { new { name = "Institución Educativa", locale = "es" } } }
                                }
                            },
                            credential_status = new
                            {
                                types = new[] { "BitstringStatusListEntry" },
                                statusPurpose = "revocation",
                                statusListCredential = $"{issuerBase.TrimEnd('/')}/status/1"
                            }
                        }
                    }
                };

                // Log del JSON que devolvemos, recortado para no llenar el disco
                var preview = System.Text.Json.JsonSerializer.Serialize(metadata);
                if (preview.Length > 500)
                    preview = preview.Substring(0, 500) + "...(truncated)";

                _logger.LogInformation("🔄 METADATA RESPONSE: {Json}", preview);

                return Ok(metadata);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error while building openid-credential-issuer metadata");
                return StatusCode(500, new { error = "metadata_generation_failed", message = ex.Message });
            }
        }

        // Inji busca el token_endpoint en estos dos endpoints de descubrimiento del AS
        [HttpGet("oauth-authorization-server")]
        [HttpGet("openid-configuration")]
        public IActionResult AuthServerMetadata([FromServices] IConfiguration cfg)
        {
            var issuerBase = cfg["Oidc4Vci:IssuerBaseUrl"]!.TrimEnd('/');
            var issuerIdentifier = cfg["Oidc4Vci:IssuerIdentifier"]!;

            var metadata = new
            {
                issuer = issuerIdentifier,
                token_endpoint = $"{issuerBase}/token",
                grant_types_supported = new[] { "urn:ietf:params:oauth:grant-type:pre-authorized_code" },
                pre_authorized_grant_anonymous_access_supported = true
            };

            _logger.LogInformation("🔄 AUTH SERVER METADATA RESPONSE for {Path}", Request.Path);
            return Ok(metadata);
        }
    }
}
