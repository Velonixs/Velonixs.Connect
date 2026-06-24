using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Infrastructure.Services;

public sealed class BusinessService(IRestaurantService restaurantService) : IBusinessService
{
    public async Task<IReadOnlyCollection<BusinessResponse>> GetBusinessesAsync(CancellationToken cancellationToken = default)
    {
        var restaurants = await restaurantService.GetRestaurantsAsync(cancellationToken);
        return restaurants.Select(ToBusinessResponse).ToArray();
    }

    public async Task<BusinessResponse?> GetBusinessAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var restaurant = await restaurantService.GetRestaurantAsync(id, cancellationToken);
        return restaurant is null ? null : ToBusinessResponse(restaurant);
    }

    public async Task<BusinessResponse> CreateBusinessAsync(CreateBusinessRequest request, CancellationToken cancellationToken = default)
    {
        var restaurant = await restaurantService.CreateRestaurantAsync(
            new CreateRestaurantRequest(
                request.Name,
                request.BusinessType,
                request.WhatsAppPhoneNumberId,
                request.BusinessPhone,
                request.NotificationEmail,
                request.StaffWhatsAppNumber,
                request.Address,
                0,
                0,
                request.IsActive),
            cancellationToken);

        return ToBusinessResponse(restaurant);
    }

    public async Task<BusinessResponse?> UpdateBusinessAsync(Guid id, UpdateBusinessRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await restaurantService.GetRestaurantAsync(id, cancellationToken);

        if (existing is null)
        {
            return null;
        }

        var restaurant = await restaurantService.UpdateRestaurantAsync(
            id,
            new UpdateRestaurantRequest(
                request.Name,
                request.BusinessType,
                request.WhatsAppPhoneNumberId,
                request.BusinessPhone,
                request.NotificationEmail,
                request.StaffWhatsAppNumber,
                request.Address,
                existing.CgstPercent,
                existing.SgstPercent,
                request.IsActive),
            cancellationToken);

        return restaurant is null ? null : ToBusinessResponse(restaurant);
    }

    private static BusinessResponse ToBusinessResponse(RestaurantResponse restaurant)
    {
        return new BusinessResponse(
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
