using Minedu.VC.Issuer.Data.Entities;
using Minedu.VC.Issuer.Models;
using Minedu.VC.Issuer.Services.Cardano;
using Minedu.VC.Issuer.Data.Repositories;
using CardanoSharp.Wallet.Models.Transactions;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using System.Security.Cryptography;

namespace Minedu.VC.Issuer.Services.Cardano
{
    public class CardanoAnchorService
    {
        private readonly CardanoTxGenerator _txGenerator;
        private readonly CardanoTxSubmitter _txSubmitter;
        private readonly IVerifiableCredentialRepository _anchorRepo;
        private readonly IConfiguration _config;
        private readonly StatusListService _statusSvc;
        private readonly ILogger<CardanoAnchorService> _logger;

        public CardanoAnchorService(
            CardanoTxGenerator txGenerator,
            CardanoTxSubmitter txSubmitter,
            IVerifiableCredentialRepository anchorRepo,
            IConfiguration config,
            StatusListService statusSvc,
            ILogger<CardanoAnchorService> logger)
        {
            _txGenerator = txGenerator;
            _txSubmitter = txSubmitter;
            _anchorRepo = anchorRepo;
            _config = config;
            _statusSvc = statusSvc;
            _logger = logger;
        }

        public async Task<string> AnchorAsync(VerifiableCredential vc, int idSolicitud)
        {
            _logger.LogInformation("Inicia funcion de anclaje de la credencial. (AnchorAsync)");
            string txHash = string.Empty;
            long fee = 0;
            string vcHash = string.Empty;
            string vcJson = string.Empty;
            string errorMessage = string.Empty;

            try
            {
                _logger.LogInformation("Calcula hash de la VC convertida a JSON");
                // Calcula hash de la VC JSON
                vcJson = System.Text.Json.JsonSerializer.Serialize(vc);
                vcHash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(vcJson)));
                _logger.LogInformation("vcHash={vcHash}", vcHash);

                // Genera y envía TX
                _logger.LogInformation("Genera Tx firmada lista para enviarse a la blockchain. (_txGenerator.GenerateSignedTxAsync)");
                string signedTxHex = await _txGenerator.GenerateSignedTxAsync(
                    _config["Cardano:receiverAddress"],
                    vcHash);
                _logger.LogInformation("signedTxHex={signedTxHex}", signedTxHex);

                _logger.LogInformation("Se envía la Tx firmada a la blockchain. (_txSubmitter.SubmitSignedTxAsync)");
                txHash = await _txSubmitter.SubmitSignedTxAsync(signedTxHex);
                _logger.LogInformation("txHash={txHash}", txHash);

                var txBytes = Convert.FromHexString(signedTxHex);
                var tx = txBytes.DeserializeTransaction();
                fee = unchecked((long)tx.TransactionBody.Fee);
                _logger.LogInformation("fee={fee}", fee);

                // Crear registro de anclaje
                _logger.LogInformation("Se crea objeto VerifiableCredentialEntity con información de la VC para guardarlo en la BD.");
                var anchored = new VerifiableCredentialEntity
                {
                    IdSolicitud = idSolicitud, // puedes enlazar al idSolicitud si lo pasas como parámetro
                    VcId = vc.Id,
                    CredentialType = string.Join(",", vc.Type),
                    IssuerDid = vc.Issuer,
                    Network = _config["Blockfrost:Network"],
                    TxHash = txHash.Trim('"'),
                    Fee = fee,
                    SenderAddress = _config["Cardano:senderAddress"],
                    AnchoredAt = DateTime.UtcNow,
                    ProofType = vc.Proof.Type,
                    VerificationMethod = vc.Proof.VerificationMethod,
                    Jws = vc.Proof.Jws,
                    VcHash = vcHash,
                    VcJson = vcJson,
                    Status = "Anchored",
                    StatusListIndex = vc.CredentialStatus?.StatusListIndex,
                    CreateTime = DateTime.UtcNow
                };
                _logger.LogInformation("Se invoca metodo para guardar la VC en la BD. (_anchorRepo.AddAsync)");
                await _anchorRepo.AddAsync(anchored);

                _logger.LogInformation("Credencial Verificable anclada correctamente.");
                return txHash;
            }
            catch (Exception ex)
            {
                // --- Propagar la excepción principal para el controlador ---
                _logger.LogError($"Error anclando la credencial (Solicitud {idSolicitud}): {ex.Message}" +
                    (ex.InnerException != null ? $". Excepción interna: {ex.InnerException.Message}" : string.Empty));
                throw new Exception(
                    $"Error anclando la credencial (Solicitud {idSolicitud}): {ex.Message}" +
                    (ex.InnerException != null ? $". Excepción interna: {ex.InnerException.Message}" : string.Empty),
                    ex
                );
            }
        }

        public async Task<string> AnchorStatusListAsync(string signedVcJson)
        {
            try
            {
                // 1. Calcular hash SHA-256
                var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(signedVcJson));
                var statusListHash = Convert.ToHexString(hashBytes).ToLowerInvariant();

                // 2. Generar y enviar TX en Cardano (usa el hash como metadata)
                string signedTxHex = await _txGenerator.GenerateSignedTxAsync(
                    _config["Cardano:receiverAddress"],
                    statusListHash);

                string txHash = await _txSubmitter.SubmitSignedTxAsync(signedTxHex);

                var txBytes = Convert.FromHexString(signedTxHex);
                var tx = txBytes.DeserializeTransaction();
                ulong fee = tx.TransactionBody.Fee;

                // 3. Guardar un pequeño registro JSON opcional
                var logPath = Path.Combine(AppContext.BaseDirectory, "Data", "statuslist-anchor-log.json");
                var logEntry = new
                {
                    AnchoredAt = DateTime.UtcNow,
                    Network = _config["Blockfrost:Network"],
                    TxHash = txHash,
                    Fee = fee,
                    SenderAddress = _config["Cardano:senderAddress"],
                    StatusListHash = statusListHash
                };

                await System.IO.File.WriteAllTextAsync(logPath, System.Text.Json.JsonSerializer.Serialize(logEntry));

                return txHash;
            }
            catch (Exception ex)
            {
                throw new Exception($"Error al anclar la lista de estado: {ex.Message}", ex);
            }
        }
    }
}
