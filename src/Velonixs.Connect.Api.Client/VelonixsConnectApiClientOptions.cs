namespace Velonixs.Connect.Api.Client;

public sealed class VelonixsConnectApiClientOptions
{
    public Uri? BaseAddress { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
}
