using System.ComponentModel.DataAnnotations;

namespace Velonixs.Connect.Application.Models;

public sealed record StaffUserResponse(
    Guid Id,
    Guid? BusinessId,
    string DisplayName,
    string Email,
    string Role,
    bool IsActive);

public sealed record CreateStaffUserRequest(
    [Required]
    string DisplayName,

    [Required]
    [EmailAddress]
    string Email,

    [Required]
    [MinLength(8)]
    string Password,

    [Required]
    string Role);

public sealed record CreateStaffUserResult(
    bool Succeeded,
    StaffUserResponse? User,
    IReadOnlyCollection<string> Errors)
{
    public static CreateStaffUserResult Success(StaffUserResponse user) => new(true, user, Array.Empty<string>());
    public static CreateStaffUserResult Failed(IEnumerable<string> errors) => new(false, null, errors.ToArray());
}

public sealed record UpdateStaffStatusResult(
    bool Succeeded,
    StaffUserResponse? User,
    IReadOnlyCollection<string> Errors)
{
    public static UpdateStaffStatusResult Success(StaffUserResponse user) => new(true, user, Array.Empty<string>());
    public static UpdateStaffStatusResult Failed(IEnumerable<string> errors) => new(false, null, errors.ToArray());
}

public sealed record UpdateStaffStatusRequest(bool IsActive);
