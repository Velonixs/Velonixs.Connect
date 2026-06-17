using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Authorization;
using Velonixs.Connect.Application;
using Velonixs.Connect.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var requirePortalAuthentication = builder.Configuration.GetValue<bool>("Portal:RequireAuthentication");

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
    });
builder.Services.AddControllersWithViews(options =>
{
    if (requirePortalAuthentication)
    {
        options.Filters.Add(new AuthorizeFilter());
    }
});
builder.Services.AddHealthChecks();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/portal/error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Portal}/{action=Index}/{id?}");

app.Run();
