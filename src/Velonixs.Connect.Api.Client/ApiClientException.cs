using System.Net;
using Velonixs.Connect.Contracts.Common;

namespace Velonixs.Connect.Api.Client;

public sealed class ApiClientException(
    HttpStatusCode statusCode,
    ApiErrorResponse? error,
    string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public ApiErrorResponse? Error { get; } = error;
}
