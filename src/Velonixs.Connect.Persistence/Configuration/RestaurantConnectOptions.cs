namespace Velonixs.Connect.Persistence.Configuration;

public sealed class RestaurantConnectOptions
{
    public bool AutoMigrateDatabase { get; set; } = true;
    public bool SeedDemoData { get; set; } = true;
    public string DemoWhatsAppPhoneNumberId { get; set; } = "local-phone-number-id";
}
