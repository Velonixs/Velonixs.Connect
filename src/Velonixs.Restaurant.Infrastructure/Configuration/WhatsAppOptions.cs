namespace Velonixs.Restaurant.Infrastructure.Configuration;

public sealed class WhatsAppOptions
{
    public string ApiVersion { get; set; } = "v20.0";
    public string? AccessToken { get; set; }
    public string? VerifyToken { get; set; }
    public string? AppSecret { get; set; }
    public string BaseUrl { get; set; } = "https://graph.facebook.com";
    public bool DisableSending { get; set; } = true;
}
