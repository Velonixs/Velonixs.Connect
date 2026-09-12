using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Velonixs.Connect.Application;
using Velonixs.Connect.Infrastructure;
using Velonixs.Connect.Persistence.Identity;
using Velonixs.Connect.Shared.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Velonixs.Connect.Portal";
        options.LoginPath = "/portal/login";
        options.LogoutPath = "/portal/logout";
        options.AccessDeniedPath = "/portal/login";
        options.Events.OnValidatePrincipal = async context =>
        {
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var businessClaim = context.Principal?.FindFirstValue(AppClaimTypes.BusinessId);
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = string.IsNullOrWhiteSpace(userId) ? null : await userManager.FindByIdAsync(userId);

            if (user is null ||
                !user.IsActive ||
                user.BusinessId is null ||
                !string.Equals(user.BusinessId.Value.ToString(), businessClaim, StringComparison.OrdinalIgnoreCase) ||
                !(await userManager.GetRolesAsync(user)).Any(AppRoles.PortalRoleNames.Contains))
            {
                context.RejectPrincipal();
            }
        };
    });
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
});
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHealthChecks();

var app = builder.Build();

app.UseExceptionHandler("/portal/error");
app.UseStatusCodePagesWithReExecute("/portal/error", "?statusCode={0}");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapGet("/portal", () => Results.Redirect("/portal/dashboard"))
    .RequireAuthorization(policy => policy.RequireRole(AppRoles.PortalRoleNames));
app.MapGet("/portal/blazor", () => Results.Redirect("/portal/dashboard"))
    .RequireAuthorization(policy => policy.RequireRole(AppRoles.PortalRoleNames));
app.MapGet("/portal/blazor/{page}", (string page) => Results.Redirect($"/portal/{page}"))
    .RequireAuthorization(policy => policy.RequireRole(AppRoles.PortalRoleNames));
app.MapRazorComponents<Velonixs.Connect.Portal.Components.App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization(policy => policy.RequireRole(AppRoles.PortalRoleNames));
app.MapControllers();

app.Run();
