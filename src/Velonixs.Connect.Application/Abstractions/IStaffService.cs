using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IStaffService
{
    Task<IReadOnlyCollection<StaffUserResponse>> GetBusinessStaffAsync(
        Guid businessId,
        CancellationToken cancellationToken = default);

    Task<CreateStaffUserResult> CreateStaffUserAsync(
        Guid businessId,
        CreateStaffUserRequest request,
        CancellationToken cancellationToken = default);

    Task<UpdateStaffStatusResult> UpdateStaffStatusAsync(
        Guid businessId,
        Guid userId,
        bool isActive,
        Guid? currentUserId = null,
        CancellationToken cancellationToken = default);
}
