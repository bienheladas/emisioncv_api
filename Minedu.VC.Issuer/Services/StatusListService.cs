using Minedu.VC.Issuer.Data.Repositories;
using Minedu.VC.Issuer.Models;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Minedu.VC.Issuer.Services
{
    public class StatusListService
    {
        private readonly string _filePath;
        private readonly IConfiguration _config;
        private readonly SignatureService _signature;
        private readonly ILogger<StatusListService> _logger;
        private readonly StatusListRepository _repo;
        private readonly IVerifiableCredentialRepository _vcRepo;

        // 131072 bits = 16384 bytes (16 KB) mínimo normativo
        private const int MinimumBits = 131072;

        public StatusListService( IConfiguration config
                                , SignatureService signature
                                , StatusListRepository repo
                                , IVerifiableCredentialRepository vcRepo
                                , ILogger<StatusListService> logger)
        {
            _config = config;
            _signature = signature;
            _repo = repo;
            _vcRepo = vcRepo;
            _logger = logger;
            _filePath = Path.Combine(_config["Logging:LogPath"], "Data", "statuslist.json");
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        }

        /// <summary>
        /// Devuelve el índice disponible más bajo (primer bit en 0) o el siguiente libre.
        /// </summary>
        public async Task<int> AllocateIndexAsync()
        {
            var bits = await LoadAsync();
            int index = bits.FindIndex(b => !b);
            if (index == -1)
            {
                // expandir si llegamos al tamaño mínimo actual
                bits.Add(false);
                index = bits.Count - 1;
            }
            await SaveAsync(bits);
            return index;
        }

        /// <summary>
        /// Marca un índice como revocado (bit = 1)
        /// </summary>
        public async Task RevokeAsync(int index)
        {
            var bits = await LoadAsync();
            if (index < 0 || index >= bits.Count)
                throw new InvalidOperationException("RANGE_ERROR: índice fuera de rango.");

            if (bits[index] == true)
                throw new InvalidOperationException("RANGE_ERROR: la credencial ya se encuentra revocada.");

            bits[index] = true;
            await SaveAsync(bits);

            // ✅ Actualizar el estado persistente
            await _vcRepo.UpdateStatusAsync(index, "Revoked");
            _logger.LogInformation($"Estado de credencial (índice {index}) actualizado a 'Revoked' en BD.");
        }

        /// <summary>
        /// Genera una credencial BitstringStatusListCredential firmada.
        /// </summary>
        public async Task<string> GetStatusListCredentialAsync()
        {
            try
            {
                var bits = await LoadAsync();
                var issuer = _config["Oidc4Vci:IssuerBaseUrl"] ?? throw new InvalidOperationException("IssuerBaseUrl faltante");

                // Generar bitstring comprimida (gzip + base64url)
                var encoded = EncodeCompressedBitstring(bits);

                var vc = new BitstringStatusListCredential
                {
                    Context = new[] { "https://www.w3.org/ns/credentials/v2" },
                    Id = $"{issuer}/status/1",
                    Type = new[] { "VerifiableCredential", "BitstringStatusListCredential" },
                    Issuer = _config["Issuer:Did"],
                    IssuanceDate = DateTime.UtcNow,
                    CredentialSubject = new BitstringStatusListSubject
                    {
                        Id = $"{issuer}/status/1#list",
                        Type = "BitstringStatusList",
                        StatusPurpose = "revocation",
                        EncodedList = encoded
                    }
                };

                // Firmar la lista de estado con el mismo método que las VC
                //var signed = _signature.SignGeneric(vc);

                // === NUEVO: Firmar la lista con firma desacoplada ===
                var json = JsonSerializer.Serialize(vc, new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
                });

                var dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
                Directory.CreateDirectory(dataDir);
                await File.WriteAllTextAsync(Path.Combine(dataDir, "statuslist-payload-emisor.json"), json);

                var proof = await _signature.CreateJws2020DetachedProof(json);

                vc.Proof = new Proof
                {
                    Type = proof["type"].ToString(),
                    Created = proof["created"].ToString(),
                    ProofPurpose = proof["proofPurpose"].ToString(),
                    VerificationMethod = proof["verificationMethod"].ToString(),
                    Jws = proof["jws"].ToString()
                };

                // Serializar final
                return JsonSerializer.Serialize(vc, new JsonSerializerOptions { WriteIndented = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al generar la lista de estado.");
                throw new InvalidOperationException("MALFORMED_VALUE_ERROR: no se pudo generar la lista de estado.");
            }
        }

        public async Task EnsureListExistsAsync()
        {
            _logger.LogInformation("Inicia metodo EnsureListExistsAsync.");
            _logger.LogInformation("_filePath = {_filePath}", _filePath);

            if (File.Exists(_filePath))
            {
                _logger.LogInformation("Lista de estado ya existe, no se requiere inicialización.");
                return;
            }

            _logger.LogWarning("No se encontró archivo de lista de estado. Intentando reconstrucción híbrida...");

            // Usar la lógica híbrida del repositorio
            var bits = await _repo.LoadAsync();

            // Si LoadAsync() reconstruyó desde BD y encontró revocados, ya guardó el archivo.
            // Si no hay revocados, _repo.LoadAsync() devuelve una lista vacía y crea el archivo en blanco.
            if (bits.All(b => !b))
                _logger.LogInformation("Lista creada en blanco (sin revocaciones registradas).");
            else
                _logger.LogInformation("Lista reconstruida desde BD con revocaciones previas.");
        }

        // ===============================================================
        //  Helpers normativos
        // ===============================================================

        private Task<List<bool>> LoadAsync() => _repo.LoadAsync();

        private Task SaveAsync(List<bool> bits) => _repo.SaveAsync(bits);

        private static string EncodeCompressedBitstring(List<bool> bits)
        {
            // Convertir bits → bytes
            int len = (bits.Count + 7) / 8;
            var bytes = new byte[len];
            for (int i = 0; i < bits.Count; i++)
                if (bits[i])
                    bytes[i / 8] |= (byte)(1 << (7 - (i % 8)));

            // Comprimir con GZIP
            using var ms = new MemoryStream();
            using (var gzip = new GZipStream(ms, CompressionLevel.SmallestSize, leaveOpen: true))
            {
                gzip.Write(bytes, 0, bytes.Length);
            }
            var compressed = ms.ToArray();

            // Codificar Base64URL (sin relleno)
            string b64 = Convert.ToBase64String(compressed)
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

            return b64;
        }

        private async Task SaveVersionedCopyAsync(List<bool> bits)
        {
            try
            {
                var ts = DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ");
                var versionPath = Path.Combine(AppContext.BaseDirectory, "Data", $"statuslist-{ts}.json");
                var json = JsonSerializer.Serialize(bits);
                await File.WriteAllTextAsync(versionPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"No se pudo guardar versión con timestamp: {ex.Message}");
            }
        }

        public async Task<string> GenerateSignedFromBitsAsync(List<bool> bits)
        {
            var issuer = _config["Oidc4Vci:IssuerBaseUrl"]!;
            var encoded = EncodeCompressedBitstring(bits);

            var vc = new BitstringStatusListCredential
            {
                Context = new[] { "https://www.w3.org/ns/credentials/v2" },
                Id = $"{issuer}/status/1",
                Type = new[] { "VerifiableCredential", "BitstringStatusListCredential" },
                Issuer = _config["Issuer:Did"],
                IssuanceDate = DateTime.UtcNow,
                CredentialSubject = new BitstringStatusListSubject
                {
                    Id = $"{issuer}/status/1#list",
                    Type = "BitstringStatusList",
                    StatusPurpose = "revocation",
                    EncodedList = encoded
                }
            };

            //var signed = _signature.SignGeneric(vc);
            var json = JsonSerializer.Serialize(vc, new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            });
            var proof = await _signature.CreateJws2020DetachedProof(json);

            vc.Proof = new Proof
            {
                Type = proof["type"].ToString(),
                Created = proof["created"].ToString(),
                ProofPurpose = proof["proofPurpose"].ToString(),
                VerificationMethod = proof["verificationMethod"].ToString(),
                Jws = proof["jws"].ToString()
            };

            return JsonSerializer.Serialize(vc);
        }
    }
}
