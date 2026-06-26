using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Persistence.Persistence;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class RestaurantService(RestaurantConnectDbContext dbContext) : IRestaurantService
{
    public async Task<IReadOnlyCollection<RestaurantResponse>> GetRestaurantsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Restaurants
            .OrderBy(x => x.Name)
            .Select(x => ToResponse(x))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<RestaurantResponse?> GetRestaurantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Restaurants
            .Where(x => x.Id == id)
            .Select(x => ToResponse(x))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<RestaurantResponse> CreateRestaurantAsync(CreateRestaurantRequest request, CancellationToken cancellationToken = default)
    {
        ValidateTaxPercent(request.CgstPercent, nameof(request.CgstPercent));
        ValidateTaxPercent(request.SgstPercent, nameof(request.SgstPercent));
        var whatsAppPhoneNumberId = request.WhatsAppPhoneNumberId.Trim();

        if (await dbContext.Restaurants.AnyAsync(x => x.WhatsAppPhoneNumberId == whatsAppPhoneNumberId, cancellationToken))
        {
            throw new InvalidOperationException("Another business already uses this WhatsApp Phone Number ID.");
        }

        var restaurant = new Domain.Entities.Restaurant
        {
            Name = request.Name.Trim(),
            BusinessType = BusinessTypes.Normalize(request.BusinessType),
            WhatsAppPhoneNumberId = whatsAppPhoneNumberId,
            BusinessPhone = request.BusinessPhone?.Trim(),
            NotificationEmail = request.NotificationEmail?.Trim(),
            StaffWhatsAppNumber = request.StaffWhatsAppNumber?.Trim(),
            Address = request.Address?.Trim(),
            CgstPercent = request.CgstPercent,
            SgstPercent = request.SgstPercent,
            IsActive = request.IsActive
        };

        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(restaurant);
    }

    public async Task<RestaurantResponse?> UpdateRestaurantAsync(Guid id, UpdateRestaurantRequest request, CancellationToken cancellationToken = default)
    {
        var whatsAppPhoneNumberId = request.WhatsAppPhoneNumberId.Trim();

        if (await dbContext.Restaurants.AnyAsync(
                x => x.Id != id && x.WhatsAppPhoneNumberId == whatsAppPhoneNumberId,
                cancellationToken))
        {
            throw new InvalidOperationException("Another business already uses this WhatsApp Phone Number ID.");
        }

        var restaurant = await dbContext.Restaurants.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (restaurant is null)
        {
            return null;
        }

        restaurant.Name = request.Name.Trim();
        restaurant.BusinessType = BusinessTypes.Normalize(request.BusinessType);
        restaurant.WhatsAppPhoneNumberId = whatsAppPhoneNumberId;
        restaurant.BusinessPhone = request.BusinessPhone?.Trim();
        restaurant.NotificationEmail = request.NotificationEmail?.Trim();
        restaurant.StaffWhatsAppNumber = request.StaffWhatsAppNumber?.Trim();
        restaurant.Address = request.Address?.Trim();
        ValidateTaxPercent(request.CgstPercent, nameof(request.CgstPercent));
        ValidateTaxPercent(request.SgstPercent, nameof(request.SgstPercent));
        restaurant.CgstPercent = request.CgstPercent;
        restaurant.SgstPercent = request.SgstPercent;
        restaurant.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(restaurant);
    }

    public async Task<RestaurantResponse?> UpdateRestaurantTaxSettingAsync(
        Guid id,
        decimal cgstPercent,
        decimal sgstPercent,
        CancellationToken cancellationToken = default)
    {
        ValidateTaxPercent(cgstPercent, nameof(cgstPercent));
        ValidateTaxPercent(sgstPercent, nameof(sgstPercent));

        var restaurant = await dbContext.Restaurants.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (restaurant is null)
        {
            return null;
        }

        restaurant.CgstPercent = cgstPercent;
        restaurant.SgstPercent = sgstPercent;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(restaurant);
    }

    public async Task<RestaurantResponse?> UpdateRestaurantAvailabilityAsync(
        Guid id,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var restaurant = await dbContext.Restaurants.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (restaurant is null)
        {
            return null;
        }

        restaurant.IsActive = isActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(restaurant);
    }

    public async Task<PlatformTaxSettingResponse> GetPlatformTaxSettingAsync(CancellationToken cancellationToken = default)
    {
        var setting = await GetOrCreatePlatformTaxSettingAsync(cancellationToken);
        return new PlatformTaxSettingResponse(setting.CgstPercent, setting.SgstPercent, setting.UpdatedAtUtc);
    }

    public async Task<PlatformTaxSettingResponse> UpdatePlatformTaxSettingAsync(
        decimal cgstPercent,
        decimal sgstPercent,
        CancellationToken cancellationToken = default)
    {
        ValidateTaxPercent(cgstPercent, nameof(cgstPercent));
        ValidateTaxPercent(sgstPercent, nameof(sgstPercent));

        var setting = await GetOrCreatePlatformTaxSettingAsync(cancellationToken);
        setting.CgstPercent = cgstPercent;
        setting.SgstPercent = sgstPercent;
        setting.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        return new PlatformTaxSettingResponse(setting.CgstPercent, setting.SgstPercent, setting.UpdatedAtUtc);
    }

    private async Task<PlatformTaxSetting> GetOrCreatePlatformTaxSettingAsync(CancellationToken cancellationToken)
    {
        var setting = await dbContext.PlatformTaxSettings.FirstOrDefaultAsync(
            x => x.Id == PlatformTaxSetting.DefaultId,
            cancellationToken);

        if (setting is not null)
        {
            return setting;
        }

        setting = new PlatformTaxSetting
        {
            Id = PlatformTaxSetting.DefaultId
        };
        dbContext.PlatformTaxSettings.Add(setting);
        await dbContext.SaveChangesAsync(cancellationToken);
        return setting;
    }

    private static RestaurantResponse ToResponse(Domain.Entities.Restaurant restaurant)
    {
        return new RestaurantResponse(
            restaurant.Id,
            restaurant.Name,
            restaurant.BusinessType,
            restaurant.WhatsAppPhoneNumberId,
            restaurant.BusinessPhone,
            restaurant.NotificationEmail,
            restaurant.StaffWhatsAppNumber,
            restaurant.Address,
            restaurant.CgstPercent,
            restaurant.SgstPercent,
            restaurant.IsActive,
            restaurant.CreatedAt);
    }

    private static void ValidateTaxPercent(decimal value, string fieldName)
    {
        if (value is < 0 or > 100)
        {
            throw new InvalidOperationException($"{fieldName} must be between 0 and 100.");
        }
    }
}
