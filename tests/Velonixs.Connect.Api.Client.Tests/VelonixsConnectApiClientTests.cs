using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Velonixs.Connect.Api.Client;
using Velonixs.Connect.Application.Models;
using Xunit;

namespace Velonixs.Connect.Api.Client.Tests;

public sealed class VelonixsConnectApiClientTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task LoginAsync_SavesReturnedTokens()
    {
        var store = new InMemoryApiTokenStore();
        var tokenResponse = CreateTokenResponse("access-token", "refresh-token");
        var client = CreateClient(store, request =>
        {
            Assert.Equal("/api/v1/auth/login", request.RequestUri!.AbsolutePath);
            return JsonResponse(tokenResponse);
        });

        var response = await client.LoginAsync(new LoginRequest("owner@example.com", "Password123"));
        var tokenState = await store.GetAsync();

        Assert.Equal("access-token", response.AccessToken);
        Assert.Equal("access-token", tokenState!.AccessToken);
        Assert.Equal("refresh-token", tokenState.RefreshToken);
    }

    [Fact]
    public async Task GetRestaurantDashboardAsync_RefreshesOnceAfterUnauthorized()
    {
        var store = new InMemoryApiTokenStore();
        await store.SaveAsync(new ApiClientTokenState(
            "expired-access-token",
            "refresh-token",
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(7)));

        var calls = new List<HttpRequestMessage>();
        var dashboard = new RestaurantDashboardResponse(
            new RestaurantResponse(
                Guid.NewGuid(),
                "99 Restaurant",
                "restaurant",
                "phone-id",
                null,
                null,
                null,
                null,
                0,
                0,
                true,
                DateTimeOffset.UtcNow),
            Array.Empty<OrderSummaryResponse>(),
            null,
            0,
            0,
            0,
            0,
            Array.Empty<CustomerSummaryResponse>());

        var client = CreateClient(store, request =>
        {
            calls.Add(CloneRequest(request));

            if (request.RequestUri!.AbsolutePath == "/api/v1/dashboard/summary" &&
                request.Headers.Authorization?.Parameter == "expired-access-token")
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }

            if (request.RequestUri.AbsolutePath == "/api/v1/auth/refresh")
            {
                return JsonResponse(CreateTokenResponse("fresh-access-token", "fresh-refresh-token"));
            }

            Assert.Equal("fresh-access-token", request.Headers.Authorization?.Parameter);
            return JsonResponse(dashboard);
        });

        var response = await client.GetRestaurantDashboardAsync();
        var tokenState = await store.GetAsync();

        Assert.Equal(dashboard.Restaurant.Id, response.Restaurant.Id);
        Assert.Equal("fresh-access-token", tokenState!.AccessToken);
        Assert.Contains(calls, request => request.RequestUri!.AbsolutePath == "/api/v1/auth/refresh");
    }

    private static VelonixsConnectApiClient CreateClient(
        IApiTokenStore tokenStore,
        Func<HttpRequestMessage, HttpResponseMessage> handler)
    {
        var httpClient = new HttpClient(new StubHttpMessageHandler(handler))
        {
            BaseAddress = new Uri("https://connect.test/")
        };

        return new VelonixsConnectApiClient(httpClient, tokenStore);
    }

    private static AuthTokenResponse CreateTokenResponse(string accessToken, string refreshToken)
    {
        return new AuthTokenResponse(
            accessToken,
            refreshToken,
            "Bearer",
            DateTimeOffset.UtcNow.AddMinutes(15),
            DateTimeOffset.UtcNow.AddDays(30),
            "owner@example.com",
            "Owner",
            Guid.NewGuid(),
            ["BusinessOwner"]);
    }

    private static HttpResponseMessage JsonResponse<T>(T value)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(value, options: JsonOptions)
        };
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(handler(request));
        }
    }
}
