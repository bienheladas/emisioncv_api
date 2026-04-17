using Blockfrost.Api.Services;
using CardanoSharp.Wallet;
using CardanoSharp.Wallet.Enums;
using CardanoSharp.Wallet.Extensions.Models;
using CardanoSharp.Wallet.Extensions.Models.Transactions;
using CardanoSharp.Wallet.Models.Addresses;
using CardanoSharp.Wallet.Models.Keys;
using CardanoSharp.Wallet.TransactionBuilding;

namespace Minedu.VC.Issuer.Services.Cardano
{
    public class CardanoTxGenerator
    {
        private readonly ICardanoService _cardano;
        private readonly string _mnemonic;
        private readonly ILogger<CardanoTxGenerator> _logger;

        public CardanoTxGenerator(string mnemonic, ICardanoService cardano, ILogger<CardanoTxGenerator> logger)
        {
            _mnemonic = mnemonic;
            _cardano = cardano;
            _logger = logger;
        }

        public async Task<string> GenerateSignedTxAsync(string receiverAddress, string vcHash)
        {
            _logger.LogInformation("Inicia funcion que genera Tx firmada. (CardanoTxGenerator.GenerateSignedTxAsync)");
            _logger.LogInformation("_mnemonic = {_mnemonic}", _mnemonic);
            _logger.LogInformation("receiverAddress = {receiverAddress}", receiverAddress);

            _logger.LogInformation("Calcula rootkey en base a mnemónico.");
            var mnemonic = new MnemonicService().Restore(_mnemonic, WordLists.English);
            var rootKey = mnemonic.GetRootKey();

            _logger.LogInformation("Calcula paymentKey en base a rootKey.");
            var paymentKey = rootKey.Derive()
                .Derive(PurposeType.Shelley)
                .Derive(CoinType.Ada)
                .Derive(0)
                .Derive(RoleType.ExternalChain)
                .Derive(0);

            _logger.LogInformation("Calcula stakeKey en base a rootKey.");
            var stakeKey = rootKey.Derive()
                .Derive(PurposeType.Shelley)
                .Derive(CoinType.Ada)
                .Derive(0)
                .Derive(RoleType.Staking)
                .Derive(0);

            _logger.LogInformation("Calcula direccion del remitente de la Tx.");
            var addressService = new CardanoSharp.Wallet.AddressService();
            var senderAddress = addressService.GetBaseAddress(paymentKey.PublicKey, stakeKey.PublicKey, NetworkType.Preprod);

            // --- Obtener UTxOs ---
            _logger.LogInformation("Obtiene Tx's disponibles para gastar en la Tx de emisión de la VC.");
            var utxos = await _cardano.Addresses.GetUtxosAsync(senderAddress.ToString());
            if (!utxos.Any())
            {
                _logger.LogInformation("El remitente no tiene UTxOs. Enviar fondos a la dirección del remitente primero.");
                throw new Exception("El remitente no tiene UTxOs. Enviar fondos a la dirección del remitente primero.");
            }

            _logger.LogInformation("Selecciona la primera Tx disponible para gastar.");
            var utxo = utxos.First();
            ulong inputAmount = ulong.Parse(utxo.Amount.First(a => a.Unit == "lovelace").Quantity);
            ulong amountToSend = 1000000; // 1 ADA
            _logger.LogInformation("inputAmount = {inputAmount}", inputAmount);
            _logger.LogInformation("amountToSend = {amountToSend}", amountToSend);

            // --- Parámetros de red ---
            var latestEpoch = await _cardano.Epochs.GetLatestAsync();
            var protocolParams = await _cardano.Epochs.GetParametersAsync((int)latestEpoch.Epoch);
            var latestBlock = await _cardano.Blocks.GetLatestAsync();
            _logger.LogInformation("latestEpoch = {latestEpoch}", latestEpoch);
            _logger.LogInformation("protocolParams = {protocolParams}", protocolParams);
            _logger.LogInformation("latestBlock = {latestBlock}", latestBlock);

            // --- Construcción inicial del body ---
            _logger.LogInformation("Construye cuerpo de la Tx.");
            var bodyBuilder = TransactionBodyBuilder.Create;
            bodyBuilder.AddInput(utxo.TxHash, (uint)utxo.TxIndex);
            bodyBuilder.AddOutput(new Address(receiverAddress), amountToSend);
            bodyBuilder.SetTtl((uint)(latestBlock.Slot + 1000));

            // --- Metadata: incluye hash de la VC ---
            _logger.LogInformation("Construye la metada el hash de la VC como metadata de la Tx.");
            var metadata = new { vcHash = vcHash };
            var auxDataBuilder = AuxiliaryDataBuilder.Create.AddMetadata(1234, metadata);

            // --- Crear testigos provisionales ---
            _logger.LogInformation("Define testigos provisionales de la Tx.");
            var witnesses = TransactionWitnessSetBuilder.Create
                .AddVKeyWitness(paymentKey.PublicKey, paymentKey.PrivateKey);

            // --- Construir transacción provisional (sin fee) ---
            _logger.LogInformation("Construye Tx de anclaje de la VC con el cuerpo, testigo y metadata.");
            var tempTx = TransactionBuilder.Create
                .SetBody(bodyBuilder)
                .SetWitnesses(witnesses)
                .SetAuxData(auxDataBuilder)
                .Build();

            // --- Calcular tamaño y fee ---
            _logger.LogInformation("Calcula tamaño y comisión de la Tx.");
            uint txSize = (uint)tempTx.Serialize().Length;
            ulong minFee = (ulong)(protocolParams.MinFeeA * txSize + protocolParams.MinFeeB);
            // Margen de seguridad de 5000 lovelace
            minFee += 5000;
            _logger.LogInformation("txSize = {txSize}", txSize);
            _logger.LogInformation("minFee = {minFee}", minFee);

            // --- Ajustar outputs y fee reales ---
            ulong change = inputAmount - amountToSend - minFee;
            if (change <= 0)
            {
                _logger.LogError("No cuenta con suficiente balance: necesita por lo menos {amountToSend} lovelace.", minFee + amountToSend);
                throw new Exception($"No cuenta con suficiente balance: necesita por lo menos {minFee + amountToSend} lovelace.");
            }

            _logger.LogInformation("Se agrega la salida y la comision al cuerpo de la Tx.");
            bodyBuilder.AddOutput(senderAddress, change);
            bodyBuilder.SetFee(minFee);

            // --- Construir transacción final ---
            _logger.LogInformation("Se construye la Tx final para el anclaje de la Tx con la comision y salida correctas.");
            var finalTx = TransactionBuilder.Create
                .SetBody(bodyBuilder)
                .SetWitnesses(witnesses)
                .SetAuxData(auxDataBuilder)
                .Build();

            _logger.LogInformation("Se serializa la Tx final firmada.");
            var signedTxHex = BitConverter.ToString(finalTx.Serialize()).Replace("-", "").ToLower();

            _logger.LogInformation("Se ha construido correctamente la Tx firmada" +
                ".");
            return signedTxHex;
        }
    }
}
