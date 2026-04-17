using Minedu.VC.Issuer.Data.Repositories;
using Minedu.VC.Issuer.Models;
using Minedu.VC.Issuer.Models.Dto;
using Minedu.VC.Issuer.Services.Mapper;

namespace Minedu.VC.Issuer.Services
{
    public class RequestService
    {
        private readonly IRequestRepository _repository;

        public RequestService(IRequestRepository repository)
        {
            _repository = repository;
        }

        public async Task<RequestAggregateDto> GetSolicitudAsync(int idSolicitud, CancellationToken ct = default)
        {
            var entity = await _repository.GetSolicitudAggregateAsync(idSolicitud, ct);

            if (entity == null) return null;

            return RequestMapper.ToAggregate(entity);
        }

        public async Task<bool> CredentialAlreadyAnchoredAsync(int idSolicitud)
        {
            return await _repository.ExistsBySolicitudAsync(idSolicitud);
        }
    }
}
