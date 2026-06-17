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
        var restaurant = new Domain.Entities.Restaurant
        {
            Name = request.Name.Trim(),
            BusinessType = BusinessTypes.Normalize(request.BusinessType),
            WhatsAppPhoneNumberId = request.WhatsAppPhoneNumberId.Trim(),
            BusinessPhone = request.BusinessPhone?.Trim(),
            NotificationEmail = request.NotificationEmail?.Trim(),
            StaffWhatsAppNumber = request.StaffWhatsAppNumber?.Trim(),
            Address = request.Address?.Trim(),
            IsActive = request.IsActive
        };

        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(restaurant);
    }

    public async Task<RestaurantResponse?> UpdateRestaurantAsync(Guid id, UpdateRestaurantRequest request, CancellationToken cancellationToken = default)
    {
        var restaurant = await dbContext.Restaurants.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (restaurant is null)
        {
            return null;
        }

        restaurant.Name = request.Name.Trim();
        restaurant.BusinessType = BusinessTypes.Normalize(request.BusinessType);
        restaurant.WhatsAppPhoneNumberId = request.WhatsAppPhoneNumberId.Trim();
        restaurant.BusinessPhone = request.BusinessPhone?.Trim();
        restaurant.NotificationEmail = request.NotificationEmail?.Trim();
        restaurant.StaffWhatsAppNumber = request.StaffWhatsAppNumber?.Trim();
        restaurant.Address = request.Address?.Trim();
        restaurant.IsActive = request.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        return ToResponse(restaurant);
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
            restaurant.IsActive,
            restaurant.CreatedAt);
    }
}
