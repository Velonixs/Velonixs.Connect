using Velonixs.Connect.Mobile.Abstractions;

namespace Velonixs.Connect.Portal.Maui.Services;

public sealed class MauiSecureTokenStorage : ISecureTokenStorage
{
    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return SecureStorage.Default.GetAsync(key);
        }
        catch
        {
            return Task.FromResult<string?>(null);
        }
    }

    public async Task SetAsync(string key, string value, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch
        {
            SecureStorage.Default.Remove(key);
        }
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            SecureStorage.Default.Remove(key);
        }
        catch
        {
            // Treat storage cleanup failures as already cleared so app startup/logout can proceed.
        }

        return Task.CompletedTask;
    }
}
