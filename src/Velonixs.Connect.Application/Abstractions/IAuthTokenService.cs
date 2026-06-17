using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IAuthTokenService
{
    Task<AuthTokenResponse?> CreateTokenAsync(LoginRequest request, CancellationToken cancellationToken = default);
}
