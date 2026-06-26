using System.ComponentModel.DataAnnotations;

namespace Velonixs.Connect.Application.Models;

public sealed record LoginRequest(
    [Required]
    [EmailAddress]
    string Email,

    [Required]
    string Password);

public sealed record AuthTokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    DateTimeOffset ExpiresAt,
    DateTimeOffset RefreshTokenExpiresAt,
    string Email,
    string DisplayName,
    Guid? BusinessId,
    IReadOnlyCollection<string> Roles);

public sealed record RefreshTokenRequest(
    [Required]
    string RefreshToken);

public sealed record LogoutRequest(
    [Required]
    string RefreshToken);
