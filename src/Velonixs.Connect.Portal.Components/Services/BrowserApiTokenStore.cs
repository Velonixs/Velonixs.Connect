using System.Text.Json;
using Microsoft.JSInterop;
using Velonixs.Connect.Api.Client;

namespace Velonixs.Connect.Portal.Components.Services;

public sealed class BrowserApiTokenStore(IJSRuntime jsRuntime) : IApiTokenStore
{
    private const string StorageKey = "velonixs.connect.tokens";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApiClientTokenState?> GetAsync(CancellationToken cancellationToken = default)
    {
        var json = await jsRuntime.InvokeAsync<string?>(
            "localStorage.getItem",
            cancellationToken,
            StorageKey);

        return string.IsNullOrWhiteSpace(json)
            ? null
            : JsonSerializer.Deserialize<ApiClientTokenState>(json, JsonOptions);
    }

    public async Task SaveAsync(ApiClientTokenState tokenState, CancellationToken cancellationToken = default)
    {
        await jsRuntime.InvokeVoidAsync(
            "localStorage.setItem",
            cancellationToken,
            StorageKey,
            JsonSerializer.Serialize(tokenState, JsonOptions));
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        await jsRuntime.InvokeVoidAsync(
            "localStorage.removeItem",
            cancellationToken,
            StorageKey);
    }
}
