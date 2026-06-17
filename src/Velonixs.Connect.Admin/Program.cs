using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using Velonixs.Connect.Application;
using Velonixs.Connect.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var requireAdminAuthentication = builder.Configuration.GetValue<bool>("Admin:RequireAuthentication");

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
    });
builder.Services.AddControllersWithViews(options =>
{
    if (requireAdminAuthentication)
    {
        options.Filters.Add(new AuthorizeFilter());
    }
});
builder.Services.AddHealthChecks();

var app = builder.Build();
var allowRemoteAdmin = builder.Configuration.GetValue<bool>("Admin:AllowRemote");

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/admin/error");
}

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
