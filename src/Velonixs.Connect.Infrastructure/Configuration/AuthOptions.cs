namespace Velonixs.Connect.Infrastructure.Configuration;

public sealed class AuthOptions
{
    public bool RequireAuthentication { get; set; }
    public string Issuer { get; set; } = "Velonixs.Connect";
    public string Audience { get; set; } = "Velonixs.Connect";
    public string SigningKey { get; set; } = string.Empty;
    public int TokenMinutes { get; set; } = 120;
}
