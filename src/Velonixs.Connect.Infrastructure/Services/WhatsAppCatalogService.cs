using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class WhatsAppCatalogService(
    RestaurantConnectDbContext dbContext,
    IWhatsAppMessageSender messageSender) : IWhatsAppCatalogService
{
    public async Task<CatalogMessageSendResult> SendCatalogMessageAsync(
        Guid restaurantId,
        string customerPhoneNumber,
        bool isTest = false,
        CancellationToken cancellationToken = default)
    {
        if (restaurantId == Guid.Empty)
        {
            throw new ArgumentException("Restaurant is required.", nameof(restaurantId));
        }

        if (string.IsNullOrWhiteSpace(customerPhoneNumber))
        {
            throw new ArgumentException("Customer phone number is required.", nameof(customerPhoneNumber));
        }

        var restaurant = await dbContext.Restaurants
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == restaurantId && x.IsActive, cancellationToken)
            ?? throw new KeyNotFoundException("Restaurant was not found or is inactive.");
        var setting = await dbContext.MetaCatalogSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == restaurantId, cancellationToken);

        if (setting is not { IsEnabled: true, IsCartEnabled: true } ||
            string.IsNullOrWhiteSpace(setting.CatalogId) ||
            string.IsNullOrWhiteSpace(setting.PhoneNumberId) ||
            !string.Equals(setting.PhoneNumberId, restaurant.WhatsAppPhoneNumberId, StringComparison.Ordinal))
        {
            return new CatalogMessageSendResult(
                false,
                false,
                restaurantId,
                Error: "The restaurant catalog, cart, and WhatsApp phone connection must be enabled before sending a catalog.");
        }

        var thumbnailRetailerId = await dbContext.MenuItems
            .AsNoTracking()
            .Where(item => item.RestaurantId == restaurantId &&
                           item.IsActive &&
                           item.IsAvailable &&
                           item.Category.IsActive &&
                           item.SyncStatus == "Synced" &&
                           item.ProductRetailerId != null &&
                           item.ProductRetailerId != "" &&
                           item.MetaProductId != null &&
                           item.MetaProductId != "")
            .OrderBy(item => item.ItemCode)
            .Select(item => item.ProductRetailerId)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(thumbnailRetailerId))
        {
            return new CatalogMessageSendResult(
                false,
                false,
                restaurantId,
                Error: "At least one active, available, synchronized catalog product is required.");
        }

        var body = isTest
            ? $"Test {restaurant.Name}'s menu. Add products to the native WhatsApp cart and send the complete cart when ready."
            : $"Welcome to {restaurant.Name}! Browse our menu and order directly from WhatsApp.";
        var result = await messageSender.SendCatalogMessageAsync(
            restaurant.WhatsAppPhoneNumberId,
            customerPhoneNumber,
            body,
            thumbnailRetailerId,
            isTest ? "Test catalog" : "Only active and in-stock products are shown.",
            cancellationToken);

        return new CatalogMessageSendResult(
            result.IsSuccess,
            result.IsSkipped,
            restaurantId,
            result.ProviderMessageId,
            result.Error);
    }
}
