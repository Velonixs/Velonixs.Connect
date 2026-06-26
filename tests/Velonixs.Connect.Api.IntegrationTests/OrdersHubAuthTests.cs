using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Velonixs.Connect.Api.Hubs;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.IntegrationTests;

public sealed class OrdersHubAuthTests(ApiWebApplicationFactory factory)
    : IClassFixture<ApiWebApplicationFactory>
{
    [Fact]
    public async Task Negotiate_rejects_unauthenticated_clients()
    {
        using var client = factory.CreateClient();

        var response = await client.PostAsync("/hubs/orders/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Negotiate_accepts_valid_restaurant_scoped_token()
    {
        using var client = factory.CreateClient();
        var restaurantId = Guid.NewGuid();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/hubs/orders/negotiate?negotiateVersion=1");
        request.Headers.Authorization = new("Bearer", CreateToken(restaurantId));

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Restaurant_groups_are_scoped_by_restaurant_id()
    {
        var firstRestaurantId = Guid.NewGuid();
        var secondRestaurantId = Guid.NewGuid();

        var firstGroup = OrdersHub.RestaurantGroup(firstRestaurantId);
        var secondGroup = OrdersHub.RestaurantGroup(secondRestaurantId);

        Assert.StartsWith("restaurant:", firstGroup);
        Assert.NotEqual(firstGroup, secondGroup);
        Assert.Contains(firstRestaurantId.ToString("N"), firstGroup);
    }

    private static string CreateToken(Guid restaurantId)
    {
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("integration-test-signing-key-0001"));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: "velonixs-connect-tests",
            audience: "velonixs-connect-tests",
            claims:
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "Integration Test User"),
                new Claim(ClaimTypes.Role, AppRoles.Staff),
                new Claim(AppClaimTypes.BusinessId, restaurantId.ToString())
            ],
            expires: DateTime.UtcNow.AddMinutes(10),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
