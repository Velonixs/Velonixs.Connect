using Microsoft.AspNetCore.Mvc.ModelBinding;
using Velonixs.Connect.Contracts.Common;

namespace Velonixs.Connect.Api.Errors;

public static class ApiErrorResponses
{
    public static ApiErrorResponse Validation(
        HttpContext httpContext,
        ModelStateDictionary modelState)
    {
        var errors = modelState
            .Where(entry => entry.Value?.Errors.Count > 0)
            .ToDictionary(
                entry => entry.Key,
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? "The value is invalid."
                        : error.ErrorMessage)
                    .ToArray());

        return Validation(httpContext, errors);
    }

    public static ApiErrorResponse Validation(
        HttpContext httpContext,
        IReadOnlyDictionary<string, string[]> validationErrors)
    {
        return new ApiErrorResponse(
            "validation_failed",
            "One or more validation errors occurred.",
            httpContext.TraceIdentifier,
            validationErrors);
    }

    public static ApiErrorResponse Failure(
        HttpContext httpContext,
        string code,
        string message)
    {
        return new ApiErrorResponse(code, message, httpContext.TraceIdentifier);
    }
}
