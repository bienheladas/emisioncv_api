using Minedu.VC.Issuer.Data.Entities;
using Minedu.VC.Issuer.Models;

namespace Minedu.VC.Issuer.Data.Repositories
{
    public interface IRequestRepository
    {
        /// <summary>
        /// Loads a full aggregate for a given MINEDU request (Solicitud), including
        /// Student, Grade (CERT_GRADO), Notes (CERT_NOTA) and Observations.
        /// </summary>
        public Task<RequestEntity?> GetSolicitudAggregateAsync(int idSolicitud, CancellationToken ct = default);

        Task<bool> ExistsBySolicitudAsync(int idSolicitud);
    }
}
