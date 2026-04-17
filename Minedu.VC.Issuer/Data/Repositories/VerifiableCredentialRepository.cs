using Minedu.VC.Issuer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Minedu.VC.Issuer.Data.Repositories
{
    public class VerifiableCredentialRepository : IVerifiableCredentialRepository
    {
        private readonly MineduDbContext _context;

        public VerifiableCredentialRepository(MineduDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(VerifiableCredentialEntity entity)
        {
            _context.AnchoredCredentials.Add(entity);
            await _context.SaveChangesAsync();
        }

        public async Task<List<int>> GetRevokedIndexesAsync()
        {
            return await _context.AnchoredCredentials
                .Where(c => c.Status == "Revoked" && c.StatusListIndex != null)
                .Select(c => Convert.ToInt32(c.StatusListIndex))
                .ToListAsync();
        }

        public async Task UpdateStatusAsync(int index, string newStatus)
        {
            var entity = await _context.AnchoredCredentials
                .FirstOrDefaultAsync(c => c.StatusListIndex == index);
            if (entity == null)
                throw new InvalidOperationException($"No se encontró credencial con índice {index}.");

            entity.Status = newStatus;
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetNextStatusListIndexAsync()
        {
            var indexes = await _context.AnchoredCredentials
                .Where(c => c.StatusListIndex != null)
                .Select(c => c.StatusListIndex.Value)
                .ToListAsync();

            if (indexes.Count == 0)
                return 0;

            return indexes.Max() + 1;
        }
    }
}
