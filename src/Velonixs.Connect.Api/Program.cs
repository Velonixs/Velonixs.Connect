using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Application;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Infrastructure;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Persistence.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddMetaCatalogBackgroundProcessing();
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ApiBusinessAccessService>();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
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
    app.UseSwagger();
    app.UseSwaggerUI();
}

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider
        .GetRequiredService<ILoggerFactory>()
        .CreateLogger("Startup");
    try
    {
        var whatsAppOptions = scope.ServiceProvider.GetRequiredService<IOptions<WhatsAppOptions>>().Value;
        if (!app.Environment.IsDevelopment() && !whatsAppOptions.DisableSending)
        {
            var missingLiveWhatsAppSettings = new List<string>();
            if (string.IsNullOrWhiteSpace(whatsAppOptions.AccessToken))
            {
                missingLiveWhatsAppSettings.Add("WhatsApp:AccessToken");
            }

            if (string.IsNullOrWhiteSpace(whatsAppOptions.AppSecret))
            {
                missingLiveWhatsAppSettings.Add("WhatsApp:AppSecret");
            }

            if (string.IsNullOrWhiteSpace(whatsAppOptions.VerifyToken))
            {
                missingLiveWhatsAppSettings.Add("WhatsApp:VerifyToken");
            }

            if (missingLiveWhatsAppSettings.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Live WhatsApp sending requires: {string.Join(", ", missingLiveWhatsAppSettings)}.");
            }
        }

        var databaseInitializer = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
        await databaseInitializer.InitializeAsync();
    }
    catch (Exception ex)
    {
        logger.LogCritical(ex, "API startup failed while initializing the database.");
        throw;
    }
}

app.UseExceptionHandler(errorApp =>
{
    errorApp.Run(async context =>
    {
        var exceptionFeature = context.Features.Get<IExceptionHandlerFeature>();
        var exception = exceptionFeature?.Error;
        var logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("GlobalExceptionHandler");
        var statusCode = exception switch
        {
            ArgumentException => StatusCodes.Status400BadRequest,
            InvalidOperationException => StatusCodes.Status400BadRequest,
            UnauthorizedAccessException => StatusCodes.Status403Forbidden,
            KeyNotFoundException => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status500InternalServerError
        };

        logger.LogError(
            exception,
            "Unhandled API exception. TraceId={TraceId}, Path={Path}, StatusCode={StatusCode}",
            context.TraceIdentifier,
            context.Request.Path,
            statusCode);

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode == StatusCodes.Status500InternalServerError
                ? "An unexpected error occurred."
                : "The request could not be processed.",
            Detail = app.Environment.IsDevelopment() ? exception?.Message : null,
            Instance = context.Request.Path
        };
        problem.Extensions["traceId"] = context.TraceIdentifier;

        await context.Response.WriteAsJsonAsync(problem);
    });
});

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
