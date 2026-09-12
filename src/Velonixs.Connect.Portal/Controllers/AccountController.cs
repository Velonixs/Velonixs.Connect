using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Portal.Models;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Portal.Controllers;

[AllowAnonymous]
public sealed class AccountController(UserManager<ApplicationUser> userManager) : Controller
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

        var user = await userManager.FindByEmailAsync(model.Email.Trim());

        var roles = user is null ? [] : await userManager.GetRolesAsync(user);

        if (user is null ||
            !user.IsActive ||
            user.BusinessId is null ||
            !await userManager.CheckPasswordAsync(user, model.Password) ||
            !roles.Any(role => AppRoles.StaffAssignable.Contains(role) || role == AppRoles.BusinessOwner))
        {
            TempData["Error"] = "This account cannot access the Business Portal.";
            return View(model);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            BuildPrincipal(user, roles),
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

    [HttpGet("portal/logout")]
    public async Task<IActionResult> LogoutFromLink()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private static ClaimsPrincipal BuildPrincipal(ApplicationUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email ?? string.Empty : user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
            new Claim(AppClaimTypes.BusinessId, user.BusinessId!.Value.ToString())
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
