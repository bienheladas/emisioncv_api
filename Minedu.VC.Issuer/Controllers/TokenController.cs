using Microsoft.AspNetCore.Mvc;
using Minedu.VC.Issuer.Models;
using Minedu.VC.Issuer.Models.Dto;
using Minedu.VC.Issuer.Services.Auth;

namespace Minedu.VC.Issuer.Controllers
{
    [ApiController]
    public class TokenController : ControllerBase
    {
        private readonly AuthorizationService _auth;
        private readonly ILogger<TokenController> _logger;

        public TokenController(AuthorizationService auth, ILogger<TokenController> logger) 
        { 
            _auth = auth;
            _logger = logger;
        } 

        [HttpPost("token")]
        public async Task<IActionResult> Token([FromForm] TokenRequest req) // wallet envía form-encoded
        {
            _logger.LogInformation("Inicia endpoint Token.");
            var form = await Request.ReadFormAsync();

            _logger.LogInformation(
                "TOKEN FORM RAW: {Form}",
                string.Join(", ", form.Select(kv => $"{kv.Key}={kv.Value}"))
            );

            _logger.LogInformation(
                "TOKEN DTO: grant_type={GrantType} | pre_authorized_code={Code} | user_pin={Pin}",
                req.grant_type,
                req.pre_authorized_code,
                req.user_pin
            );

            var (ok, token, exp, err) = _auth.ExchangePreAuthorizedCode(req.grant_type, req.pre_authorized_code);

            _logger.LogInformation("Resultado de ExchangePreAuthorizedCode. | ok={ok} | token={token} | exp={exp} | err={err}", ok, token, exp, err);

            if (!ok)
            {
                _logger.LogError("Falló el intercambio del código preautorizado por token.");
                return BadRequest(new { error = err }); 
            }

            _logger.LogInformation("Retorna el token correctamente.");
            return Ok(new TokenResponse
            {
                access_token = token!,
                expires_in = exp,
                token_type = "Bearer"
            });
        }
    }
}
