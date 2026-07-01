using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Infrastructure;
using Velonixs.Connect.Infrastructure.Configuration;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class WhatsAppOptionsConfigurationTests
{
    [Fact]
    public void AddInfrastructure_UsesWhatsAppEnvironmentFallbacksWhenAppSettingsAreEmpty()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:RestaurantConnect"] = "Server=(localdb)\\mssqllocaldb;Database=VelonixsConnectTest;Trusted_Connection=True;",
                ["DataEncryption:Key"] = "12345678901234567890123456789012",
                ["Auth:SigningKey"] = "12345678901234567890123456789012",
                ["WhatsApp:AccessToken"] = "",
                ["WhatsApp:VerifyToken"] = "",
                ["WhatsApp:AppSecret"] = "",
                ["WhatsApp:DisableSending"] = "true",
                ["WHATSAPP_ACCESS_TOKEN"] = "env-access-token",
                ["WHATSAPP_VERIFY_TOKEN"] = "env-verify-token",
                ["META_APP_SECRET"] = "env-app-secret",
                ["WHATSAPP_API_VERSION"] = "v21.0",
                ["WHATSAPP_DISABLE_SENDING"] = "false",
                ["WHATSAPP_SEND_TIMEOUT_SECONDS"] = "17"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<WhatsAppOptions>>().Value;

        Assert.Equal("env-access-token", options.AccessToken);
        Assert.Equal("env-verify-token", options.VerifyToken);
        Assert.Equal("env-app-secret", options.AppSecret);
        Assert.Equal("v21.0", options.ApiVersion);
        Assert.False(options.DisableSending);
        Assert.Equal(17, options.SendTimeoutSeconds);
    }
}
