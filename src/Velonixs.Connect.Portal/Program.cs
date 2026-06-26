using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application;
using Velonixs.Connect.Infrastructure;
using Velonixs.Connect.Persistence.Persistence;
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
            var accountSessionService = context.HttpContext.RequestServices.GetRequiredService<IAccountSessionService>();

            var session = Guid.TryParse(userId, out var parsedUserId) &&
                          Guid.TryParse(businessClaim, out var parsedBusinessId)
                ? await accountSessionService.ValidatePortalSessionAsync(parsedUserId, parsedBusinessId)
                : null;

            if (session is null)
            {
                context.RejectPrincipal();
            }
        };
    });
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AuthorizeFilter());
});
builder.Services.AddHealthChecks();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    try
    {
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "Portal startup failed while initializing the database.");
        throw;
    }
}

app.UseExceptionHandler("/portal/error");
app.UseStatusCodePagesWithReExecute("/portal/error", "?statusCode={0}");

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Portal}/{action=Index}/{id?}");

app.Run();
