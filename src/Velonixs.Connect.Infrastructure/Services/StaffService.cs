using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class StaffService(UserManager<ApplicationUser> userManager) : IStaffService
{
    public async Task<IReadOnlyCollection<StaffUserResponse>> GetBusinessStaffAsync(
        Guid businessId,
        CancellationToken cancellationToken = default)
    {
        var users = await userManager.Users
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.DisplayName)
            .ToArrayAsync(cancellationToken);
        var responses = new List<StaffUserResponse>(users.Length);

        foreach (var user in users)
        {
            responses.Add(await ToResponseAsync(user));
        }

        return responses;
    }

    public async Task<CreateStaffUserResult> CreateStaffUserAsync(
        Guid businessId,
        CreateStaffUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!AppRoles.StaffAssignable.Contains(request.Role))
        {
            return CreateStaffUserResult.Failed(["Select an allowed role."]);
        }

        var email = request.Email.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
        {
            return CreateStaffUserResult.Failed(["An account already exists for this email."]);
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            DisplayName = request.DisplayName.Trim(),
            BusinessId = businessId,
            IsActive = true
        };

        var result = await userManager.CreateAsync(user, request.Password);

        if (result.Succeeded)
        {
            result = await userManager.AddToRoleAsync(user, request.Role);
        }

        return result.Succeeded
            ? CreateStaffUserResult.Success(await ToResponseAsync(user))
            : CreateStaffUserResult.Failed(result.Errors.Select(x => x.Description));
    }

    public async Task<UpdateStaffStatusResult> UpdateStaffStatusAsync(
        Guid businessId,
        Guid userId,
        bool isActive,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId.ToString());

        if (user is null || user.BusinessId != businessId)
        {
            return UpdateStaffStatusResult.Failed(["Staff account was not found."]);
        }

        if (currentUserId == user.Id)
        {
            return UpdateStaffStatusResult.Failed(["You cannot deactivate your own account."]);
        }

        user.IsActive = isActive;
        var result = await userManager.UpdateAsync(user);

        return result.Succeeded
            ? UpdateStaffStatusResult.Success(await ToResponseAsync(user))
            : UpdateStaffStatusResult.Failed(result.Errors.Select(x => x.Description));
    }

    private async Task<StaffUserResponse> ToResponseAsync(ApplicationUser user)
    {
        var role = (await userManager.GetRolesAsync(user)).FirstOrDefault() ?? "Unassigned";

        return new StaffUserResponse(
            user.Id,
            user.BusinessId,
            user.DisplayName,
            user.Email ?? string.Empty,
            role,
            user.IsActive);
    }
}
