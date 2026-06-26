using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/auth")]
[Route("api/v1/auth")]
public sealed class AuthController(IAuthTokenService authTokenService) : ControllerBase
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var token = await authTokenService.CreateTokenAsync(request, cancellationToken);
        return token is null ? Unauthorized() : Ok(token);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var token = await authTokenService.RefreshTokenAsync(request, cancellationToken);
        return token is null ? Unauthorized() : Ok(token);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout(
        [FromBody] LogoutRequest request,
        CancellationToken cancellationToken)
    {
        await authTokenService.RevokeRefreshTokenAsync(request, cancellationToken);
        return NoContent();
    }
}
