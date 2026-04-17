using System.Text.Json;
using Minedu.VC.Issuer.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace Minedu.VC.Issuer.Data.Repositories
{
    public class StatusListRepository
    {
        //private readonly string _logPath;
        private readonly IConfiguration _config;
        private readonly string _filePath;
        private readonly IVerifiableCredentialRepository _vcRepo;
        private readonly ILogger<StatusListRepository> _logger;
        private const int MinimumBits = 131072;

        public StatusListRepository(IVerifiableCredentialRepository vcRepo, ILogger<StatusListRepository> logger, IConfiguration config)
        {
            _vcRepo = vcRepo;
            _logger = logger;
            _config = config;
            _filePath = Path.Combine(_config["Logging:LogPath"], "Data", "statuslist.json");
        }

        public async Task<List<bool>> LoadAsync()
        {
            if (File.Exists(_filePath))
            {
                var json = await File.ReadAllTextAsync(_filePath);
                return JsonSerializer.Deserialize<List<bool>>(json) ?? new List<bool>();
            }

            _logger.LogWarning("Archivo de lista de estado no encontrado. Reconstruyendo desde BD...");
            var bits = await RebuildFromDatabaseAsync();
            _logger.LogInformation("Se recuperaron los indices de las credenciales revocadas.");

            await SaveAsync(bits);
            _logger.LogInformation("Se grabó la lista de estado en el disco.");

            return bits;
        }

        public async Task SaveAsync(List<bool> bits)
        {
            _logger.LogInformation("Inicia función SaveAsync que grabará la lista de estados en disco.");
            if (bits.Count < MinimumBits)
                bits.AddRange(Enumerable.Repeat(false, MinimumBits - bits.Count));

            var json = JsonSerializer.Serialize(bits);
            _logger.LogInformation("Se arma el Json con la lista de bits de los estados de las VC.");
            _logger.LogInformation("Se grabará el Json de la lista de estados en la ruta. | _path={_path}", _filePath);
            await File.WriteAllTextAsync(_filePath, json);
        }

        private async Task<List<bool>> RebuildFromDatabaseAsync()
        {
            _logger.LogInformation("Inicia función RebuildFromDatabaseAsync.");
            var bits = Enumerable.Repeat(false, MinimumBits).ToList();
            _logger.LogInformation("Se consultará la BD para obtener indices revocados.");
            var revoked = await _vcRepo.GetRevokedIndexesAsync();
            _logger.LogInformation("Se consultó la BD por indices revocados correctamente.");
            foreach (var idx in revoked)
                if (idx >= 0 && idx < bits.Count)
                    bits[idx] = true;

            _logger.LogInformation($"Reconstruida lista desde BD con {revoked.Count} índices revocados.");
            return bits;
        }
    }


}
