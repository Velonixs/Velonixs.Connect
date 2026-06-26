using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.Hubs;

[Authorize]
public sealed class OrdersHub : Hub<IOrdersHubClient>
{
    public const string Path = "/hubs/orders";

    public override async Task OnConnectedAsync()
    {
        if (GetAssignedBusinessId() is Guid businessId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, RestaurantGroup(businessId));
        }

        await base.OnConnectedAsync();
    }

    public async Task SubscribeRestaurant(Guid restaurantId)
    {
        if (!CanSubscribeRestaurant(restaurantId))
        {
            throw new HubException("The current user cannot subscribe to this restaurant.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, RestaurantGroup(restaurantId));
    }

    public async Task UnsubscribeRestaurant(Guid restaurantId)
    {
        if (!CanSubscribeRestaurant(restaurantId))
        {
            throw new HubException("The current user cannot unsubscribe from this restaurant.");
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, RestaurantGroup(restaurantId));
    }

    public static string RestaurantGroup(Guid restaurantId) => $"restaurant:{restaurantId:N}";

    private bool CanSubscribeRestaurant(Guid restaurantId)
    {
        if (Context.User?.IsInRole(AppRoles.PlatformAdmin) == true)
        {
            return true;
        }

        return GetAssignedBusinessId() == restaurantId;
    }

    private Guid? GetAssignedBusinessId()
    {
        var claim = Context.User?.FindFirstValue(AppClaimTypes.BusinessId);
        return Guid.TryParse(claim, out var businessId) ? businessId : null;
    }
}
