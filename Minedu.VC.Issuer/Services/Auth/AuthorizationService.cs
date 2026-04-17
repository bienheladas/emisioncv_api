using Minedu.VC.Issuer.Controllers;
using Minedu.VC.Issuer.Services.Auth;
using System.Security.Cryptography;
using System.Text;

namespace Minedu.VC.Issuer.Services.Auth
{
    public class AuthorizationService
    {
        private readonly ITokenStore _store;
        private readonly IConfiguration _config;
        private readonly ILogger<AuthorizationService> _logger;

        private const string GrantTypePreAuth = "urn:ietf:params:oauth:grant-type:pre-authorized_code";

        public AuthorizationService(ITokenStore store, IConfiguration config, ILogger<AuthorizationService> logger)
        {
            _store = store;
            _config = config;
            _logger = logger;
        }

        public string GeneratePreAuthorizedCode(int idSolicitud, TimeSpan ttl)
        {
            var code = Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
                        .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            _store.SavePreAuth(code, new PreAuthRecord(idSolicitud, DateTime.UtcNow, false));
            return code;
        }

        public (bool ok, string? token, int expiresSeconds, string? error) ExchangePreAuthorizedCode(
            string grantType, string? code)
        {
            _logger.LogInformation("Inicia funcion ExchangePreAuthorizedCode. | grantType={grantType} | code={code}", grantType, code);

            if (grantType != GrantTypePreAuth) 
            {
                _logger.LogError("El tipo de concesión (grant) no es aceptado.");
                return (false, null, 0, "unsupported_grant_type");
            }


            if (string.IsNullOrWhiteSpace(code))
            {
                _logger.LogError("El código pre-autorizado está vacío.");
                return (false, null, 0, "invalid_request");
            }
                
            var rec = _store.GetPreAuth(code);

            if (rec is null) 
            {
                _logger.LogError("El código pre-autorizado no existe.");
                return (false, null, 0, "invalid_grant");
            }

            if (rec.Used) 
            {
                _logger.LogError("El código pre-autorizado ya ha sido usado.");
                return (false, null, 0, "invalid_grant");
            }
            

            // TTL desde config (por defecto 600s)
            var expires = _config.GetValue<int?>("Oidc4Vci:AccessTokenTtlSeconds") ?? 600;

            var token = MakeOpaqueToken(code, rec.IdSolicitud, expires);
            _store.MarkPreAuthUsed(code);
            _store.SaveAccessToken(new AccessTokenRecord(token, rec.IdSolicitud, DateTime.UtcNow.AddSeconds(expires)));

            return (true, token, expires, null);
        }

        public (bool ok, int idSolicitud) ValidateAccessToken(string bearer)
        {
            var rec = _store.GetAccessToken(bearer);
            if (rec is null) return (false, 0);
            if (rec.ExpiresUtc < DateTime.UtcNow) return (false, 0);
            return (true, rec.IdSolicitud);
        }

        private string MakeOpaqueToken(string code, int id, int expires)
        {
            // Token opaco HMAC (rápido para prototipo). En prod podrías migrar a JWT.
            var secret = _config["Oidc4Vci:TokenSigningSecret"] ?? "dev-secret-change-me";
            var payload = $"{code}.{id}.{DateTimeOffset.UtcNow.ToUnixTimeSeconds() + expires}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var sig = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)))
                        .Replace('+', '-').Replace('/', '_').TrimEnd('=');
            return $"{payload}.{sig}";
        }
    }
}
