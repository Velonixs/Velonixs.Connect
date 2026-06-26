namespace Velonixs.Connect.Contracts.Common;

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    string? TraceId = null,
    IReadOnlyDictionary<string, string[]>? ValidationErrors = null);
