namespace Velonixs.Connect.Infrastructure.Configuration;

public sealed class MetaCatalogOptions
{
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com";
    public string GraphApiVersion { get; set; } = "v26.0";
    public string Currency { get; set; } = "INR";
    public bool DisableSending { get; set; } = true;
    public int MaxRetryCount { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
    public int WorkerIntervalSeconds { get; set; } = 60;
    public int ProcessingLeaseSeconds { get; set; } = 300;

    public string GetVersionedGraphApiBaseUrl()
    {
        var baseUrl = GraphApiBaseUrl.TrimEnd('/');
        var lastSegment = Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            ? uri.Segments.LastOrDefault()?.Trim('/')
            : null;

        return lastSegment is not null &&
               lastSegment.StartsWith('v') &&
               Version.TryParse(lastSegment[1..], out _)
            ? baseUrl
            : $"{baseUrl}/{GraphApiVersion.Trim('/')}";
    }
}
