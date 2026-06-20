using Velonixs.Connect.Application;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Infrastructure;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Persistence.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ApiBusinessAccessService>();
builder.Services.AddHealthChecks();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendApps", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

using (var scope = app.Services.CreateScope())
{
    var databaseInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await databaseInitializer.InitializeAsync();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("FrontendApps");
app.UseAuthentication();
app.UseAuthorization();

if (builder.Configuration.GetSection("Auth").Get<AuthOptions>()?.RequireAuthentication == true)
{
    app.MapControllers().RequireAuthorization();
}
else
{
    app.MapControllers();
}

app.MapHealthChecks("/health");

app.Run();
