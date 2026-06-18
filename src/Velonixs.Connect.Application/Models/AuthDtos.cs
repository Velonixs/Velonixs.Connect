namespace Velonixs.Connect.Application.Models;

public sealed record LoginRequest(
    string Email,
    string Password);

public sealed record AuthTokenResponse(
    string AccessToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    string Email,
    string DisplayName,
    Guid? BusinessId,
    IReadOnlyCollection<string> Roles);
