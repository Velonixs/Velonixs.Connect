namespace Velonixs.Connect.Application.Models;

public sealed record AccountSessionResponse(
    Guid UserId,
    Guid? BusinessId,
    string Email,
    string DisplayName,
    IReadOnlyCollection<string> Roles);
