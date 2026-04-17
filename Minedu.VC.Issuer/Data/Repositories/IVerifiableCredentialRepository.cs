using Minedu.VC.Issuer.Data.Entities;

namespace Minedu.VC.Issuer.Data.Repositories
{
    public interface IVerifiableCredentialRepository
    {
        Task AddAsync(VerifiableCredentialEntity entity);
        Task<List<int>> GetRevokedIndexesAsync();
        Task UpdateStatusAsync(int index, string newStatus);
        Task<int> GetNextStatusListIndexAsync();
    }
}
