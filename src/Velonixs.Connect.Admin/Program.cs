using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
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
        options.Cookie.Name = "Velonixs.Connect.Admin";
        options.LoginPath = "/admin/login";
        options.LogoutPath = "/admin/logout";
        options.AccessDeniedPath = "/admin/login";
        options.Events.OnValidatePrincipal = async context =>
        {
            var userId = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var user = string.IsNullOrWhiteSpace(userId) ? null : await userManager.FindByIdAsync(userId);

            if (user is null || !user.IsActive || !await userManager.IsInRoleAsync(user, AppRoles.PlatformAdmin))
            {
                context.RejectPrincipal();
            }
        };
    });
builder.Services.AddControllersWithViews();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHealthChecks();

var app = builder.Build();
var allowRemoteAdmin = builder.Configuration.GetValue<bool>("Admin:AllowRemote");

app.UseExceptionHandler("/admin/blazor");

app.UseStaticFiles();

app.Use(async (context, next) =>
{
    var host = context.Request.Host.Host;
    var isLocalHost = string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                      string.Equals(host, "::1", StringComparison.OrdinalIgnoreCase);

    if (!allowRemoteAdmin && !isLocalHost)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsync("Admin UI is only available from localhost.");
        return;
    }

    await next(context);
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/admin/blazor"))
    .RequireAuthorization(policy => policy.RequireRole(AppRoles.PlatformAdmin));
app.MapGet("/admin", () => Results.Redirect("/admin/blazor"))
    .RequireAuthorization(policy => policy.RequireRole(AppRoles.PlatformAdmin));
app.MapRazorComponents<Velonixs.Connect.Admin.Components.App>()
    .AddInteractiveServerRenderMode()
    .RequireAuthorization(policy => policy.RequireRole(AppRoles.PlatformAdmin));
app.MapControllerRoute(
    name: "account",
    pattern: "{controller=Account}/{action=Login}/{id?}");

app.Run();
