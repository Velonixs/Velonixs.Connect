namespace Velonixs.Connect.Persistence.Configuration;

public sealed class IdentitySeedOptions
{
    public string? DefaultAdminEmail { get; set; }
    public string? DefaultAdminPassword { get; set; }
    public string DefaultAdminDisplayName { get; set; } = "Velonixs Admin";
}
