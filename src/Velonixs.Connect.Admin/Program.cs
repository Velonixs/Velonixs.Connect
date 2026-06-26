using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application;
using Velonixs.Connect.Infrastructure;
using Velonixs.Connect.Persistence.Persistence;

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
            var accountSessionService = context.HttpContext.RequestServices.GetRequiredService<IAccountSessionService>();
            var session = Guid.TryParse(userId, out var parsedUserId)
                ? await accountSessionService.ValidateAdminSessionAsync(parsedUserId)
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
var allowRemoteAdmin = builder.Configuration.GetValue<bool>("Admin:AllowRemote");

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
        logger.LogCritical(ex, "Admin startup failed while initializing the database.");
        throw;
    }
}

app.UseExceptionHandler("/admin/error");
app.UseStatusCodePagesWithReExecute("/admin/error", "?statusCode={0}");

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

app.MapHealthChecks("/health");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Admin}/{action=Index}/{id?}");

app.Run();
