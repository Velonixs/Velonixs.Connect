using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class AccountSessionService(UserManager<ApplicationUser> userManager) : IAccountSessionService
{
    public async Task<AccountSessionResponse?> AuthenticateAdminAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserByEmailAsync(email, cancellationToken);

        if (user is null ||
            !await userManager.CheckPasswordAsync(user, password) ||
            !await userManager.IsInRoleAsync(user, AppRoles.PlatformAdmin))
        {
            return null;
        }

        return await ToSessionAsync(user);
    }

    public async Task<AccountSessionResponse?> AuthenticatePortalAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserByEmailAsync(email, cancellationToken);
        var roles = user is null ? [] : await userManager.GetRolesAsync(user);

        if (user is null ||
            user.BusinessId is null ||
            !await userManager.CheckPasswordAsync(user, password) ||
            !roles.Any(CanAccessPortal))
        {
            return null;
        }

        return ToSession(user, roles);
    }

    public async Task<AccountSessionResponse?> ValidateAdminSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserByIdAsync(userId, cancellationToken);

        if (user is null || !await userManager.IsInRoleAsync(user, AppRoles.PlatformAdmin))
        {
            return null;
        }

        return await ToSessionAsync(user);
    }

    public async Task<AccountSessionResponse?> ValidatePortalSessionAsync(
        Guid userId,
        Guid? businessId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserByIdAsync(userId, cancellationToken);
        var roles = user is null ? [] : await userManager.GetRolesAsync(user);

        if (user is null ||
            user.BusinessId is null ||
            businessId != user.BusinessId ||
            !roles.Any(CanAccessPortal))
        {
            return null;
        }

        return ToSession(user, roles);
    }

    private async Task<ApplicationUser?> FindActiveUserByEmailAsync(
        string email,
        CancellationToken cancellationToken)
    {
        var normalizedEmail = userManager.NormalizeEmail(email.Trim());
        return await userManager.Users.FirstOrDefaultAsync(
            user => user.NormalizedEmail == normalizedEmail && user.IsActive,
            cancellationToken);
    }

    private async Task<ApplicationUser?> FindActiveUserByIdAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await userManager.Users.FirstOrDefaultAsync(
            user => user.Id == userId && user.IsActive,
            cancellationToken);
    }

    private async Task<AccountSessionResponse> ToSessionAsync(ApplicationUser user)
    {
        return ToSession(user, await userManager.GetRolesAsync(user));
    }

    private static AccountSessionResponse ToSession(
        ApplicationUser user,
        IEnumerable<string> roles)
    {
        return new AccountSessionResponse(
            user.Id,
            user.BusinessId,
            user.Email ?? string.Empty,
            string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email ?? string.Empty : user.DisplayName,
            roles.ToArray());
    }

    private static bool CanAccessPortal(string role)
    {
        return role == AppRoles.BusinessOwner || AppRoles.StaffAssignable.Contains(role);
    }
}
