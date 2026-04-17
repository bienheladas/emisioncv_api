using Microsoft.Extensions.Configuration;

namespace Minedu.VC.Issuer.Services
{
    public class DidWebResolver
    {
        private readonly IConfiguration _config;

        public DidWebResolver(IConfiguration config)
        {
            _config = config;
        }

        public string GetVerificationMethod()
        {
            var did = _config["Issuer:Did"];
            return $"{did}#keys-1"; // ejemplo: did:web:sistemas02.minedu.gob.pe#keys-1
        }
    }
}
