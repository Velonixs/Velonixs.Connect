using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IBusinessService
{
    Task<IReadOnlyCollection<BusinessResponse>> GetBusinessesAsync(CancellationToken cancellationToken = default);
    Task<BusinessResponse?> GetBusinessAsync(Guid id, CancellationToken cancellationToken = default);
    Task<BusinessResponse> CreateBusinessAsync(CreateBusinessRequest request, CancellationToken cancellationToken = default);
    Task<BusinessResponse?> UpdateBusinessAsync(Guid id, UpdateBusinessRequest request, CancellationToken cancellationToken = default);
}
