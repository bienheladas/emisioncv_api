using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Minedu.VC.Issuer.Models;
using Minedu.VC.Issuer.Services;
using Minedu.VC.Issuer.Services.Auth;
using Minedu.VC.Issuer.Services.Cardano;
using Minedu.VC.Issuer.Services.Mapper;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Minedu.VC.Issuer.Controllers
{
    [ApiController]
    [Route("issuer")]
    //[Authorize]
    public class IssuerController : ControllerBase
    {
        private readonly VCBuilder _vcBuilder;
        private readonly SignatureService _signatureService;
        private readonly RequestService _requestService;
        private readonly CardanoAnchorService _cardanoAnchorService;  // Nuevo servicio para anclaje
        private readonly StatusListService _statusSvc;
        private readonly ILogger<IssuerController> _logger;
        private readonly AuthorizationService _auth;

        public IssuerController(
                               VCBuilder vcBuilder, 
                               SignatureService signatureService,
                               RequestService requestService,
                               CardanoAnchorService cardanoAnchorService, 
                               StatusListService statusSvc,
                               ILogger<IssuerController> logger,
                               AuthorizationService auth)
        {
            _logger = logger;
            _logger.LogInformation("Iniciando constructor de IssuerController - logger ok.");
            _vcBuilder = vcBuilder;
            _logger.LogInformation("Constructor de IssuerController - vcBuilder ok.");
            _signatureService = signatureService;
            _logger.LogInformation("Constructor de IssuerController - signatureService ok.");
            _requestService = requestService;
            _logger.LogInformation("Constructor de IssuerController - requestService ok.");
            _cardanoAnchorService = cardanoAnchorService;
            _logger.LogInformation("Constructor de IssuerController - cardanoAnchorService ok.");
            _statusSvc = statusSvc;
            _logger.LogInformation("Constructor de IssuerController - statusSvc ok.");
            _auth = auth;
            _logger.LogInformation("Constructor de IssuerController - auth ok.");
        }

        [HttpPost("credential")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> IssueCredential([FromBody] JsonElement? body = null)
        {
            /* Inji llamará al credential_endpoint que declares en la well-known. Por eso lo expongo como 
             * POST /issuer/credential para ligar token ↔ solicitud de forma directa en el piloto.*/
            _logger.LogInformation("Iniciando emision de credencial para solicitud.");

            // 1) Validacion Bearer
            var authz = Request.Headers.Authorization.ToString();
            var bearer = authz?.StartsWith("Bearer ") == true ? authz.Substring("Bearer ".Length) : null;

            _logger.LogInformation("Obtiene el bearer de la petición. | bearer={bearer}", bearer);

            if (string.IsNullOrWhiteSpace(bearer))
            {
                _logger.LogError("El bearer es nulo o está en blanco.");
                return Unauthorized(new { error = "invalid_bearer" });
            }

            _logger.LogInformation("Iniciará validación del bearer.");
            var (ok, idSolicitud) = _auth.ValidateAccessToken(bearer);
            _logger.LogInformation("Culmina validación del bearer. | ok={ok} | idSolicitud={idSolicitud}", ok.ToString(), idSolicitud);

            // 2) Validacion idSolicitud
            if (idSolicitud <= 0)
            {
                _logger.LogError("El número de solicitud no es válido. | idSolicitud={idSolicitud}", idSolicitud);
                return BadRequest(new ApiResponse { Success = false, Message = "ID de solicitud inválido." });
            }

            _logger.LogInformation("Se valida si credencial para la solicitud ya existe y se encuentra anclada en blockchain. (CredentialAlreadyAnchoredAsync)");
            if (await _requestService.CredentialAlreadyAnchoredAsync(idSolicitud))
            {
                _logger.LogError("Ya existe una credencial anclada para la solicitud.");
                return BadRequest(new { success = false, message = $"Ya existe una credencial anclada para la solicitud {idSolicitud}." });
            }

            try
            {
                _logger.LogInformation("Busca la información de la solicitud en la base de datos. (GetSolicitudAsync)");
                // Step 1: obtain request data
                var aggregate = await _requestService.GetSolicitudAsync(idSolicitud);

                if (aggregate == null)
                {
                    _logger.LogError("No se encontró la solicitud con ID {idSolicitud}.", idSolicitud);
                    return NotFound(new ApiResponse
                    {
                        Success = false,
                        Message = $"No se encontró la solicitud con ID {idSolicitud}."
                    });
                }

                // Map aggregate → CredentialSubject (+ IssueContext)
                _logger.LogInformation("Convierte la información de la solicitud en el Subject de la Credencial Verificable.");
                var subject = CredentialSubjectMapper.ToSubject(aggregate);

                // Holder binding: extraer DID del wallet desde proof.jwt del request
                var holderDid = ExtractHolderDid(body);
                if (holderDid != null)
                    _logger.LogInformation("Holder DID extraído del proof.jwt: {HolderDid}", holderDid);
                else
                    _logger.LogInformation("No se encontró proof.jwt — credencial sin holder binding.");

                _logger.LogInformation("Se asegura que la lista de estados de revocación exista. (_statusSvc.EnsureListExistsAsync)");
                await _statusSvc.EnsureListExistsAsync();

                // Step 2: build VC (will be implemented in later steps)
                _logger.LogInformation("Construye la Credencial Verificable con el Subject de la solicitud. (_vcBuilder.BuildCredentialAsync)");
                var vc = await _vcBuilder.BuildCredentialAsync(subject, holderDid);

                _logger.LogInformation("Establece opciones de serialización como JSON de la credencial.");
                var credentialJson = JsonSerializer.Serialize(vc, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });
                _logger.LogInformation("Credencial Verificable armada como Json. | credentialJson={credentialJson}", credentialJson);

                // Step 3: Create proof (payload desacoplado)
                _logger.LogInformation("Crea la prueba criptografica con payload desacoplado. (_signatureService.CreateJws2020DetachedProof)");
                var proof = await _signatureService.CreateJws2020DetachedProof(credentialJson);

                // Step 4: Attach proof
                _logger.LogInformation("Agrega la prueba criptográfica a la credencial verificable de la solicitud.");
                vc.Proof = new Proof
                {
                    Type = proof["type"].ToString(),
                    Created = proof["created"].ToString(),
                    ProofPurpose = proof["proofPurpose"].ToString(),
                    VerificationMethod = proof["verificationMethod"].ToString(),
                    Jws = proof["jws"].ToString()
                };

                // Anclar en Cardano para inmovilidad
                _logger.LogInformation("Ancla la credencial verificable de la solicitud a la blockchain para inmutabilidad. (_cardanoAnchorService.AnchorAsync)");
                var txHash = await _cardanoAnchorService.AnchorAsync(vc, idSolicitud);
                var cleanTx = txHash?.Trim().Trim('"');
                _logger.LogInformation("Hash de la Tx que ancla la credencial verificable en la blockchain. | cleanTx={cleanTx}", cleanTx);

                _logger.LogInformation("Agrega la Tx que prueba el anclaje dentro de la misma credencial verificable.");
                vc.Proof.Anchor = new Anchor { TxHash = cleanTx };

                _logger.LogInformation($"Credencial emitida correctamente para solicitud {idSolicitud}.");
                return Ok(new 
                { 
                    format = "lpd_vc",
                    credential = vc
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error al emitir la credencial para la solicitud {idSolicitud}");
                return StatusCode(500, new
                {
                    error = "server_error"
                });
            }
        }

        // Extrae el DID del wallet desde proof.jwt del cuerpo del request OID4VCI
        private string? ExtractHolderDid(JsonElement? body)
        {
            try
            {
                if (body is not JsonElement b || b.ValueKind != JsonValueKind.Object) return null;
                if (!b.TryGetProperty("proof", out var proof)) return null;
                if (!proof.TryGetProperty("jwt", out var jwtEl)) return null;

                var jwt = jwtEl.GetString();
                if (string.IsNullOrEmpty(jwt)) return null;

                var parts = jwt.Split('.');
                if (parts.Length != 3) return null;

                // Decodificar header (base64url sin padding)
                var headerBytes = Base64UrlDecode(parts[0]);
                var headerJson = Encoding.UTF8.GetString(headerBytes);
                using var headerDoc = JsonDocument.Parse(headerJson);
                var header = headerDoc.RootElement;

                // Caso 1: header contiene "jwk" directamente
                if (header.TryGetProperty("jwk", out var jwkEl))
                {
                    var jwkJson = jwkEl.GetRawText();
                    var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(jwkJson))
                        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
                    return $"did:jwk:{encoded}";
                }

                // Caso 2: header contiene "kid" que ya es un DID
                if (header.TryGetProperty("kid", out var kidEl))
                {
                    var kid = kidEl.GetString();
                    if (kid?.StartsWith("did:") == true)
                        return kid.Split('#')[0];
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "No se pudo extraer el holder DID del proof.jwt — se omite holder binding.");
                return null;
            }
        }

        private static byte[] Base64UrlDecode(string input)
        {
            var s = input.Replace('-', '+').Replace('_', '/');
            s += (s.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
            return Convert.FromBase64String(s);
        }
    }
}
