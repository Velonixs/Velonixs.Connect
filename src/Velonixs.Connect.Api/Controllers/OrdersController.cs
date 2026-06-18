using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
public sealed class OrdersController(
    IOrderService orderService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpGet("api/restaurants/{restaurantId:guid}/orders")]
    [HttpGet("api/businesses/{restaurantId:guid}/orders")]
    public async Task<IActionResult> GetRestaurantOrders(Guid restaurantId, CancellationToken cancellationToken)
    {
        if (!access.CanManageOrders(restaurantId))
        {
            return Forbid();
        }

        return Ok(await orderService.GetRestaurantOrdersAsync(restaurantId, cancellationToken));
    }

    [HttpGet("api/orders/{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        if (!await access.CanReadOrderAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var order = await orderService.GetOrderAsync(id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPut("api/orders/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!await access.CanReadOrderAsync(id, cancellationToken))
        {
            return Forbid();
        }

        var order = await orderService.UpdateStatusAsync(id, request, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }
}
