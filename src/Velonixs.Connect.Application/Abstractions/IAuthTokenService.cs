using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IAuthTokenService
{
    Task<AuthTokenResponse?> CreateTokenAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokenResponse?> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken = default);
    Task<bool> RevokeRefreshTokenAsync(LogoutRequest request, CancellationToken cancellationToken = default);
}
