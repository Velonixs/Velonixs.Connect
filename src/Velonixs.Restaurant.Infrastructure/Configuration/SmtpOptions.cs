namespace Velonixs.Restaurant.Infrastructure.Configuration;

public sealed class SmtpOptions
{
    public bool EnableEmail { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; } = 587;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableSsl { get; set; } = true;
    public string? DefaultFromEmail { get; set; }
    public string? AdminEmail { get; set; }
}
