using Microsoft.Extensions.Configuration;
using Minedu.VC.Issuer.Data.Repositories;
using Minedu.VC.Issuer.Models;

namespace Minedu.VC.Issuer.Services
{
    public class VCBuilder
    {
        private readonly IConfiguration _config;
        private readonly StatusListService _statusSvc;
        private readonly IVerifiableCredentialRepository _vcRepo;


        public VCBuilder(IConfiguration config, 
                         StatusListService statusSvc,
                         IVerifiableCredentialRepository vcRepo)
        {
            _config = config;
            _statusSvc = statusSvc;
            _vcRepo = vcRepo;
        }

        public VerifiableCredential BuildCredential(CredentialSubject subject)
        {
            var vc = new VerifiableCredential
            {
                Issuer = _config["Issuer:Did"],
                IssuanceDate = DateTime.UtcNow,
                CredentialSchema = new CredentialSchema
                {
                    Id = _config["Schema:Url"],
                    Type = "JsonSchema"
                },
                CredentialSubject = subject
            };

            return vc;
        }

        public async Task<VerifiableCredential> BuildCredentialAsync(CredentialSubject subject, string? holderDid = null)
        {
            var issuerBase = _config["Oidc4Vci:IssuerBaseUrl"];
            //var index = await _statusSvc.AllocateIndexAsync();
            var index = await _vcRepo.GetNextStatusListIndexAsync();

            if (holderDid != null)
                subject.Id = holderDid;

            var vc = new VerifiableCredential
            {
                Issuer = _config["Issuer:Did"],
                IssuanceDate = DateTime.UtcNow,
                CredentialSchema = new CredentialSchema
                {
                    Id = _config["Schema:Url"],
                    Type = "JsonSchema"
                },
                CredentialSubject = subject,
                CredentialStatus = new CredentialStatus
                {
                    Id = $"{issuerBase}/status/1#{index}",
                    Type = "BitstringStatusListEntry",
                    StatusPurpose = "revocation",
                    StatusListIndex = index,
                    StatusListCredential = $"{issuerBase}/status/1"
                }
            };

            return vc;
        }
    }
}
