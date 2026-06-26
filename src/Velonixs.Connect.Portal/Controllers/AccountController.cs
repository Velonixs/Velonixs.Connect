using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Portal.Models;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Portal.Controllers;

[AllowAnonymous]
public sealed class AccountController(IAccountSessionService accountSessionService) : Controller
{
    [HttpGet("portal/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("portal/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var session = await accountSessionService.AuthenticatePortalAsync(
            model.Email,
            model.Password,
            HttpContext.RequestAborted);

        if (session is null)
        {
            TempData["Error"] = "This account cannot access the Business Portal.";
            return View(model);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            BuildPrincipal(session),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        var returnUrl = !string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? model.ReturnUrl
            : "/portal";

        return LocalRedirect(returnUrl);
    }

    [HttpPost("portal/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private static ClaimsPrincipal BuildPrincipal(AccountSessionResponse session)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, session.UserId.ToString()),
            new Claim(ClaimTypes.Name, session.DisplayName),
            new Claim(ClaimTypes.Email, session.Email),
            new Claim(AppClaimTypes.BusinessId, session.BusinessId!.Value.ToString())
        };

        claims.AddRange(session.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
