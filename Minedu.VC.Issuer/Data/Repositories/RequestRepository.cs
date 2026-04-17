using Microsoft.EntityFrameworkCore;
using Minedu.VC.Issuer.Data;
using Minedu.VC.Issuer.Data.Entities;
using Minedu.VC.Issuer.Models;
using Minedu.VC.Issuer.Services.Mapper;

namespace Minedu.VC.Issuer.Data.Repositories
{
    public class RequestRepository : IRequestRepository
    {
        private readonly MineduDbContext _context;

        public RequestRepository(MineduDbContext context)
        {
            _context = context;
        }

        public async Task<RequestEntity?> GetSolicitudAggregateAsync(int idSolicitud, CancellationToken ct = default)
        {
            // Single source of truth: fetch everything we need in one round trip.
            return await _context.Solicitudes
                .AsNoTracking()
                .Include(s => s.Estudiante)
                .Include(s => s.Grados)
                    .ThenInclude(g => g.Notas)
                .Include(s => s.Observaciones)
                .FirstOrDefaultAsync(s => s.Id == idSolicitud, ct);
        }

        public async Task<bool> ExistsBySolicitudAsync(int idSolicitud)
        {
            return await _context.AnchoredCredentials
                .AnyAsync(x => x.IdSolicitud == idSolicitud && x.Status == "Anchored");
        }
    }
}
