using Blockfrost.Api.Services;

namespace Minedu.VC.Issuer.Services.Cardano
{
    public class CardanoTxSubmitter
    {
        private readonly ICardanoService _cardano;
        private readonly string _apiKey;
        private readonly string _network;

        public CardanoTxSubmitter(ICardanoService cardano, IConfiguration config)
        {
            _cardano = cardano;
            _apiKey = config["Blockfrost:ApiKey"];
            _network = config["Blockfrost:Network"];
        }

        public async Task<string> SubmitSignedTxAsync(string signedTxHex)
        {
            var http = new HttpClient();
            http.DefaultRequestHeaders.Add("project_id", _apiKey);

            var bytes = Convert.FromHexString(signedTxHex.Trim());
            var content = new ByteArrayContent(bytes);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/cbor");

            var resp = await http.PostAsync($"https://cardano-{_network}.blockfrost.io/api/v0/tx/submit", content);
            var body = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Blockfrost TX error ({(int)resp.StatusCode}): {body}");

            return body.Trim();
        }
    }

    
}
