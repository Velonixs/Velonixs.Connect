using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Errors;
using Velonixs.Connect.Api.Hubs;
using Velonixs.Connect.Application;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Contracts.Common;
using Velonixs.Connect.Infrastructure;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Persistence.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
            new BadRequestObjectResult(ApiErrorResponses.Validation(context.HttpContext, context.ModelState));
    });
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ApiBusinessAccessService>();
builder.Services.AddScoped<IOrderRealtimeNotifier, SignalROrderRealtimeNotifier>();
builder.Services.AddSignalR();
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
            .AllowAnyMethod()
            .AllowCredentials();
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
        context.Response.ContentType = "application/json";

        var (code, message) = statusCode switch
        {
            StatusCodes.Status400BadRequest => ("bad_request", "The request could not be processed."),
            StatusCodes.Status403Forbidden => ("forbidden", "The request is not permitted."),
            StatusCodes.Status404NotFound => ("not_found", "The requested resource was not found."),
            _ => ("unexpected_error", "An unexpected error occurred.")
        };

        var response = new ApiErrorResponse(
            code,
            app.Environment.IsDevelopment() && exception is not null ? exception.Message : message,
            context.TraceIdentifier);

        await context.Response.WriteAsJsonAsync(response);
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

app.MapHub<OrdersHub>(OrdersHub.Path).RequireAuthorization();

app.MapHealthChecks("/health");

app.Run();

public partial class Program;
