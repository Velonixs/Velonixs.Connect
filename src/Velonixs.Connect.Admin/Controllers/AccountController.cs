using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Admin.Models;
using Velonixs.Connect.Persistence.Identity;

namespace Velonixs.Connect.Admin.Controllers;

[AllowAnonymous]
public sealed class AccountController(UserManager<ApplicationUser> userManager) : Controller
{
    [HttpGet("admin/login")]
    public IActionResult Login(string? returnUrl = null)
    {
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("admin/login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await userManager.FindByEmailAsync(model.Email.Trim());

        if (user is null || !await userManager.CheckPasswordAsync(user, model.Password))
        {
            TempData["Error"] = "Invalid email or password.";
            return View(model);
        }

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            BuildPrincipal(user),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        var returnUrl = !string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl)
            ? model.ReturnUrl
            : "/admin";

        return LocalRedirect(returnUrl);
    }

    [HttpPost("admin/logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    private static ClaimsPrincipal BuildPrincipal(ApplicationUser user)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email ?? string.Empty : user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email ?? string.Empty)
        };

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
