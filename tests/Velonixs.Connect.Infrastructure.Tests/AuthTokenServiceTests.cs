using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Security;
using Velonixs.Connect.Shared.Security;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class AuthTokenServiceTests
{
    [Fact]
    public async Task CreateTokenAsync_IssuesAccessAndRefreshTokens()
    {
        await using var services = await CreateServicesAsync();
        var service = CreateAuthTokenService(services);
        var dbContext = services.GetRequiredService<RestaurantConnectDbContext>();

        var token = await service.CreateTokenAsync(new LoginRequest("owner@example.com", "Password123"));

        Assert.NotNull(token);
        Assert.False(string.IsNullOrWhiteSpace(token.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(token.RefreshToken));
        Assert.True(token.RefreshTokenExpiresAt > token.ExpiresAt);
        var storedToken = await dbContext.AuthRefreshTokens.SingleAsync();
        Assert.NotEqual(token.RefreshToken, storedToken.TokenHash);
        Assert.Null(storedToken.RevokedAtUtc);
    }

    [Fact]
    public async Task RefreshTokenAsync_RotatesTokenAndRejectsReplay()
    {
        await using var services = await CreateServicesAsync();
        var service = CreateAuthTokenService(services);
        var dbContext = services.GetRequiredService<RestaurantConnectDbContext>();
        var loginToken = await service.CreateTokenAsync(new LoginRequest("owner@example.com", "Password123"));

        var refreshed = await service.RefreshTokenAsync(new RefreshTokenRequest(loginToken!.RefreshToken));
        var replay = await service.RefreshTokenAsync(new RefreshTokenRequest(loginToken.RefreshToken));

        Assert.NotNull(refreshed);
        Assert.NotEqual(loginToken.RefreshToken, refreshed.RefreshToken);
        Assert.Null(replay);
        Assert.Equal(2, await dbContext.AuthRefreshTokens.CountAsync());
        Assert.Equal(1, await dbContext.AuthRefreshTokens.CountAsync(x => x.RevokedAtUtc != null));
    }

    [Fact]
    public async Task RevokeRefreshTokenAsync_RevokesTokenAndPreventsRefresh()
    {
        await using var services = await CreateServicesAsync();
        var service = CreateAuthTokenService(services);
        var loginToken = await service.CreateTokenAsync(new LoginRequest("owner@example.com", "Password123"));

        var revoked = await service.RevokeRefreshTokenAsync(new LogoutRequest(loginToken!.RefreshToken));
        var refreshed = await service.RefreshTokenAsync(new RefreshTokenRequest(loginToken.RefreshToken));

        Assert.True(revoked);
        Assert.Null(refreshed);
    }

    private static AuthTokenService CreateAuthTokenService(ServiceProvider services) =>
        new(
            services.GetRequiredService<UserManager<ApplicationUser>>(),
            services.GetRequiredService<RestaurantConnectDbContext>(),
            Options.Create(new AuthOptions
            {
                Issuer = "Velonixs.Connect.Tests",
                Audience = "Velonixs.Connect.Tests",
                SigningKey = "0123456789abcdef0123456789abcdef",
                TokenMinutes = 15,
                RefreshTokenDays = 7
            }));

    private static async Task<ServiceProvider> CreateServicesAsync()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFieldEncryptionService, PassThroughEncryptionService>();
        services.AddDbContext<RestaurantConnectDbContext>(options =>
            options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireNonAlphanumeric = false;
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<RestaurantConnectDbContext>();

        var provider = services.BuildServiceProvider();
        var roleManager = provider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();

        foreach (var role in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
            }
        }

        var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = "owner@example.com",
            Email = "owner@example.com",
            EmailConfirmed = true,
            DisplayName = "Owner",
            IsActive = true
        };
        var result = await userManager.CreateAsync(user, "Password123");
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(x => x.Description)));
        result = await userManager.AddToRoleAsync(user, AppRoles.PlatformAdmin);
        Assert.True(result.Succeeded, string.Join(" ", result.Errors.Select(x => x.Description)));

        return provider;
    }

    private sealed class PassThroughEncryptionService : IFieldEncryptionService
    {
        public string Encrypt(string plaintext) => plaintext;
        public string Decrypt(string protectedValue) => protectedValue;
    }
}
