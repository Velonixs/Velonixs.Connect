using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Velonixs.Connect.Contracts.Common;

namespace Velonixs.Connect.Api.IntegrationTests;

public sealed class ValidationErrorContractTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Invalid_login_request_returns_api_error_response()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new
        {
            email = "not-an-email",
            password = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);

        Assert.NotNull(error);
        Assert.Equal("validation_failed", error.Code);
        Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
        Assert.Contains("Email", error.ValidationErrors!.Keys);
        Assert.Contains("Password", error.ValidationErrors.Keys);
    }

    [Fact]
    public async Task Invalid_refresh_request_returns_api_error_response()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/refresh", new
        {
            refreshToken = ""
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(JsonOptions);

        Assert.NotNull(error);
        Assert.Equal("validation_failed", error.Code);
        Assert.Contains("RefreshToken", error.ValidationErrors!.Keys);
    }
}
