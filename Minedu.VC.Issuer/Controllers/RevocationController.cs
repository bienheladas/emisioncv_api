using Microsoft.AspNetCore.Mvc;
using Minedu.VC.Issuer.Services;
using Minedu.VC.Issuer.Services.Cardano;

namespace Minedu.VC.Issuer.Controllers
{
    [Route("status/revoke")]
    [ApiController]
    public class RevocationController : ControllerBase
    {
        private readonly StatusListService _svc;
        private readonly ILogger<RevocationController> _logger;
        private readonly CardanoAnchorService _anchorSvc;

        public RevocationController(StatusListService svc, ILogger<RevocationController> logger, CardanoAnchorService anchorSvc)
        {
            _svc = svc;
            _logger = logger;
            _anchorSvc = anchorSvc;
        }

        [HttpPost("{index:int}")]
        public async Task<IActionResult> RevokeCredential(int index)
        {
            try
            {
                await _svc.RevokeAsync(index);

                // 🔄 Regenerar y firmar la nueva lista
                var signedList = await _svc.GetStatusListCredentialAsync();
                
                // 🔗 Anclar la lista actualizada en Cardano
                var txHash = await _anchorSvc.AnchorStatusListAsync(signedList);

                _logger.LogInformation($"Credencial revocada en índice {index}. Lista anclada en {txHash}");

                return Ok(new 
                { 
                    success = true, 
                    message = $"Credencial revocada (index {index})",
                    anchorTx = txHash
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, error = ex.Message });
            }
        }
    }
}
