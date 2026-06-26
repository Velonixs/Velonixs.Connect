using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Api.Client;

public sealed record ApiClientTokenState(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt);

public interface IApiTokenStore
{
    Task<ApiClientTokenState?> GetAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(ApiClientTokenState tokenState, CancellationToken cancellationToken = default);
    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed class InMemoryApiTokenStore : IApiTokenStore
{
    private ApiClientTokenState? _tokenState;

    public Task<ApiClientTokenState?> GetAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_tokenState);

    public Task SaveAsync(ApiClientTokenState tokenState, CancellationToken cancellationToken = default)
    {
        _tokenState = tokenState;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken = default)
    {
        _tokenState = null;
        return Task.CompletedTask;
    }

    public static ApiClientTokenState FromResponse(AuthTokenResponse response)
    {
        return new ApiClientTokenState(
            response.AccessToken,
            response.RefreshToken,
            response.ExpiresAt,
            response.RefreshTokenExpiresAt);
    }
}
