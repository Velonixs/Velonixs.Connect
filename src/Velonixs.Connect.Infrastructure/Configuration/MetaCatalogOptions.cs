namespace Velonixs.Connect.Infrastructure.Configuration;

public sealed class MetaCatalogOptions
{
    public string GraphApiBaseUrl { get; set; } = "https://graph.facebook.com/v25.0";
    public string Currency { get; set; } = "INR";
    public bool DisableSending { get; set; } = true;
    public int MaxRetryCount { get; set; } = 5;
    public int BatchSize { get; set; } = 20;
    public int WorkerIntervalSeconds { get; set; } = 60;
    public int ProcessingLeaseSeconds { get; set; } = 300;
}
