using Velonixs.Connect.Api.Client;
using Velonixs.Connect.Mobile.Abstractions;

namespace Velonixs.Connect.Mobile.Abstractions.Tests;

public sealed class SecureApiTokenStoreAdapterTests
{
    [Fact]
    public async Task SaveAndGetAsync_RoundTripsTokenState()
    {
        var storage = new InMemorySecureTokenStorage();
        var store = new SecureApiTokenStoreAdapter(storage);
        var tokenState = new ApiClientTokenState(
            "access-token",
            "refresh-token",
            DateTimeOffset.UtcNow.AddMinutes(15),
            DateTimeOffset.UtcNow.AddDays(14));

        await store.SaveAsync(tokenState);

        var actual = await store.GetAsync();

        Assert.Equal(tokenState, actual);
    }

    [Fact]
    public async Task ClearAsync_RemovesSavedTokenState()
    {
        var storage = new InMemorySecureTokenStorage();
        var store = new SecureApiTokenStoreAdapter(storage);
        var tokenState = new ApiClientTokenState(
            "access-token",
            "refresh-token",
            DateTimeOffset.UtcNow.AddMinutes(15),
            DateTimeOffset.UtcNow.AddDays(14));

        await store.SaveAsync(tokenState);
        await store.ClearAsync();

        Assert.Null(await store.GetAsync());
        Assert.Empty(storage.Values);
    }

    [Fact]
    public async Task GetAsync_ClearsAndReturnsNull_WhenStoredExpiryCannotBeParsed()
    {
        var storage = new InMemorySecureTokenStorage
        {
            Values =
            {
                [SecureApiTokenStoreAdapter.AccessTokenKey] = "access-token",
                [SecureApiTokenStoreAdapter.RefreshTokenKey] = "refresh-token",
                [SecureApiTokenStoreAdapter.AccessTokenExpiresAtKey] = "not-a-date",
                [SecureApiTokenStoreAdapter.RefreshTokenExpiresAtKey] = DateTimeOffset.UtcNow.AddDays(14).ToString("O")
            }
        };
        var store = new SecureApiTokenStoreAdapter(storage);

        var actual = await store.GetAsync();

        Assert.Null(actual);
        Assert.Empty(storage.Values);
    }

    private sealed class InMemorySecureTokenStorage : ISecureTokenStorage
    {
        public Dictionary<string, string> Values { get; } = [];

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        {
            Values.TryGetValue(key, out var value);
            return Task.FromResult<string?>(value);
        }

        public Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            Values.Remove(key);
            return Task.CompletedTask;
        }
    }
}
