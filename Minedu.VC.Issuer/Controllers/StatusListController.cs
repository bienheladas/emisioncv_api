using Microsoft.AspNetCore.Mvc;
using Minedu.VC.Issuer.Services;
using System.Text.Json;
using FileIO = System.IO.File;

namespace Minedu.VC.Issuer.Controllers
{

    [Route("status")]
    [ApiController]
    public class StatusListController : ControllerBase
    {
        private readonly StatusListService _svc;
        private readonly ILogger<StatusListController> _logger;

        public StatusListController(StatusListService svc, ILogger<StatusListController> logger)
        {
            _svc = svc;
            _logger = logger;
        }

        /// <summary>
        /// Devuelve la credencial BitstringStatusListCredential firmada,
        /// o una versión histórica si se pasa el parámetro timestamp.
        /// </summary>
        [HttpGet("1")]
        public async Task<IActionResult> GetStatusList([FromQuery] string? timestamp)
        {
            try
            {
                string json;

                if (!string.IsNullOrEmpty(timestamp))
                {
                    // Buscar archivo correspondiente
                    var safeTs = timestamp.Replace(":", "").Replace("-", "");
                    var versionPath = Path.Combine(AppContext.BaseDirectory, "Data", $"statuslist-{safeTs}.json");

                    if (!FileIO.Exists(versionPath))
                        return NotFound(new
                        {
                            error = "STATUS_RETRIEVAL_ERROR",
                            message = $"No existe una lista de estado para timestamp {timestamp}"
                        });

                    var bits = JsonSerializer.Deserialize<List<bool>>(await FileIO.ReadAllTextAsync(versionPath))
                                ?? new List<bool>();

                    var signed = await _svc.GenerateSignedFromBitsAsync(bits);
                    json = signed;
                }
                else
                {
                    json = await _svc.GetStatusListCredentialAsync();
                }

                Response.Headers["Cache-Control"] = "public, max-age=600"; // sugerencia W3C: cacheable
                return Content(json, "application/json");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al obtener la lista de estado");
                return StatusCode(500, new
                {
                    error = "STATUS_LIST_ERROR",
                    message = ex.Message
                });
            }
        }
    }
}
