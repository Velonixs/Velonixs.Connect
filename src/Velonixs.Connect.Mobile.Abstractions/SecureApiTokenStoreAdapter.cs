using System.Globalization;
using Velonixs.Connect.Api.Client;

namespace Velonixs.Connect.Mobile.Abstractions;

public sealed class SecureApiTokenStoreAdapter(ISecureTokenStorage secureStorage) : IApiTokenStore
{
    public const string AccessTokenKey = "velonixs.auth.access_token";
    public const string RefreshTokenKey = "velonixs.auth.refresh_token";
    public const string AccessTokenExpiresAtKey = "velonixs.auth.access_token_expires_at";
    public const string RefreshTokenExpiresAtKey = "velonixs.auth.refresh_token_expires_at";

    public async Task<ApiClientTokenState?> GetAsync(CancellationToken cancellationToken = default)
    {
        var accessToken = await secureStorage.GetAsync(AccessTokenKey, cancellationToken);
        var refreshToken = await secureStorage.GetAsync(RefreshTokenKey, cancellationToken);
        var accessTokenExpiresAt = await secureStorage.GetAsync(AccessTokenExpiresAtKey, cancellationToken);
        var refreshTokenExpiresAt = await secureStorage.GetAsync(RefreshTokenExpiresAtKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(accessToken) ||
            string.IsNullOrWhiteSpace(refreshToken) ||
            string.IsNullOrWhiteSpace(accessTokenExpiresAt) ||
            string.IsNullOrWhiteSpace(refreshTokenExpiresAt))
        {
            return null;
        }

        if (!DateTimeOffset.TryParse(
                accessTokenExpiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedAccessTokenExpiresAt) ||
            !DateTimeOffset.TryParse(
                refreshTokenExpiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsedRefreshTokenExpiresAt))
        {
            await ClearAsync(cancellationToken);
            return null;
        }

        return new ApiClientTokenState(
            accessToken,
            refreshToken,
            parsedAccessTokenExpiresAt,
            parsedRefreshTokenExpiresAt);
    }

    public async Task SaveAsync(ApiClientTokenState tokenState, CancellationToken cancellationToken = default)
    {
        await secureStorage.SetAsync(AccessTokenKey, tokenState.AccessToken, cancellationToken);
        await secureStorage.SetAsync(RefreshTokenKey, tokenState.RefreshToken, cancellationToken);
        await secureStorage.SetAsync(
            AccessTokenExpiresAtKey,
            tokenState.AccessTokenExpiresAt.ToString("O", CultureInfo.InvariantCulture),
            cancellationToken);
        await secureStorage.SetAsync(
            RefreshTokenExpiresAtKey,
            tokenState.RefreshTokenExpiresAt.ToString("O", CultureInfo.InvariantCulture),
            cancellationToken);
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await secureStorage.RemoveAsync(AccessTokenKey, cancellationToken);
        await secureStorage.RemoveAsync(RefreshTokenKey, cancellationToken);
        await secureStorage.RemoveAsync(AccessTokenExpiresAtKey, cancellationToken);
        await secureStorage.RemoveAsync(RefreshTokenExpiresAtKey, cancellationToken);
    }
}
