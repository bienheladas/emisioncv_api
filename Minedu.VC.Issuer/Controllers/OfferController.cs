using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Minedu.VC.Issuer.Models;
using Minedu.VC.Issuer.Models.Dto;
using Minedu.VC.Issuer.Services;
using System.Text.Json;

namespace Minedu.VC.Issuer.Controllers
{
    [Route("offer")]
    [ApiController]
    public class OfferController : ControllerBase
    {
        private readonly CredentialOfferService _svc;
        private readonly ILogger<OfferController> _logger;

        public OfferController(CredentialOfferService svc, ILogger<OfferController> logger) 
        { 
            _svc = svc; 
            _logger = logger;
        }
            

        // Para generar una offer vinculada a un idSolicitud: devuelve la URI para QR
        [HttpPost]
        public IActionResult CreateOffer([FromQuery] int idSolicitud)
        {
            if (idSolicitud <= 0)
                return BadRequest(new ApiResponse { Success = false, Message = "idSolicitud inválido" });

            var (_, _, offerUri) = _svc.CreateOfferForSolicitud(idSolicitud);
            var deepLink = $"openid-credential-offer://?credential_offer_uri={Uri.EscapeDataString(offerUri)}";

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "Offer creada",
                Data = new { credential_offer_uri = offerUri, deeplink = deepLink }
            });
        }

        // Endpoint público que consume Inji (credential_offer_uri)
        [HttpGet("{code}")]
        public IActionResult GetOffer(string code, [FromServices] IConfiguration config)
        {
            _logger.LogInformation("Inicia endpoint GetOffer. code={code}", code);

            var callerIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var ua = Request.Headers["User-Agent"].ToString();

            _logger.LogInformation(
                """
                🔍 GetOffer REQUEST RECEIVED
                From IP: {Ip}
                User-Agent: {UA}
                """,
                callerIp, ua
            );

            // Reconstruimos el objeto para este code (stateless para el prototipo)
            // Si deseas, podrías validar que el code exista en el store (opcional aquí).
            var issuerBase = config["Oidc4Vci:IssuerBaseUrl"]!;
            var issuerIdentifier = config["Oidc4Vci:IssuerIdentifier"]!;
            var credConfigId = config["Oidc4Vci:CredentialConfigurationId"] ?? "certificado-estudios-vc";

            _logger.LogInformation("issuerBase: {issuerBase} | credConfigId={credConfigId}", issuerBase, credConfigId);

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

            _logger.LogInformation("Se creó la oferta de credencial");

            // Ajuste de nombre JSON para el grant type exacto
            var opts = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
            var json = JsonSerializer.SerializeToElement(offer, opts);

            _logger.LogInformation("Se serializa la oferta de credencial en JSON. | json = {json}", json);

            // Reemplace el nombre de la propiedad si deseas 100% literal del grant
            // Aquí devolvemos tal cual con camelCase (Inji lee bien si el contenido es correcto).
            return new JsonResult(json);
        }
    }
}
