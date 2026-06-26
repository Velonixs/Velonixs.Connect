using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Persistence.Persistence;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class AuthTokenService(
    UserManager<ApplicationUser> userManager,
    RestaurantConnectDbContext dbContext,
    IOptions<AuthOptions> options) : IAuthTokenService
{
    private readonly AuthOptions _options = options.Value;

    public async Task<AuthTokenResponse?> CreateTokenAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());

        if (user is null || !user.IsActive || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return null;
        }

        return await CreateTokenForUserAsync(user, cancellationToken);
    }

    public async Task<AuthTokenResponse?> RefreshTokenAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var now = DateTimeOffset.UtcNow;
        var refreshToken = await dbContext.AuthRefreshTokens
            .Include(x => x.User)
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is null || !refreshToken.IsActive(now) || !refreshToken.User.IsActive)
        {
            return null;
        }

        var response = await CreateTokenForUserAsync(refreshToken.User, cancellationToken);
        refreshToken.RevokedAtUtc = now;
        refreshToken.ReplacedByTokenHash = HashToken(response.RefreshToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return response;
    }

    public async Task<bool> RevokeRefreshTokenAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var tokenHash = HashToken(request.RefreshToken);
        var refreshToken = await dbContext.AuthRefreshTokens
            .FirstOrDefaultAsync(x => x.TokenHash == tokenHash, cancellationToken);

        if (refreshToken is null)
        {
            return false;
        }

        if (refreshToken.RevokedAtUtc is null)
        {
            refreshToken.RevokedAtUtc = DateTimeOffset.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    private async Task<AuthTokenResponse> CreateTokenForUserAsync(
        ApplicationUser user,
        CancellationToken cancellationToken)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(Math.Max(5, _options.TokenMinutes));
        var refreshTokenExpiresAt = DateTimeOffset.UtcNow.AddDays(Math.Max(1, _options.RefreshTokenDays));
        var refreshToken = CreateRefreshToken();
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };

        if (user.BusinessId is Guid businessId)
        {
            claims.Add(new Claim(AppClaimTypes.BusinessId, businessId.ToString()));
        }

        var roles = await userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            _options.Issuer,
            _options.Audience,
            claims,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        dbContext.AuthRefreshTokens.Add(new AuthRefreshToken
        {
            UserId = user.Id,
            TokenHash = HashToken(refreshToken),
            ExpiresAtUtc = refreshTokenExpiresAt
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return new AuthTokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            refreshToken,
            "Bearer",
            expiresAt,
            refreshTokenExpiresAt,
            user.Email ?? string.Empty,
            user.DisplayName,
            user.BusinessId,
            roles.ToArray());
    }

    private static string CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Base64UrlEncoder.Encode(bytes);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
