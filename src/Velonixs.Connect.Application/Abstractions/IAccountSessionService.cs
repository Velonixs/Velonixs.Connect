using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Application.Abstractions;

public interface IAccountSessionService
{
    Task<AccountSessionResponse?> AuthenticateAdminAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<AccountSessionResponse?> AuthenticatePortalAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default);

    Task<AccountSessionResponse?> ValidateAdminSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AccountSessionResponse?> ValidatePortalSessionAsync(
        Guid userId,
        Guid? businessId,
        CancellationToken cancellationToken = default);
}
