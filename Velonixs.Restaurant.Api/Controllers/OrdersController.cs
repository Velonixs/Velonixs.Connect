using Microsoft.AspNetCore.Mvc;
using Velonixs.Restaurant.Application.Abstractions;
using Velonixs.Restaurant.Application.Models;

namespace Velonixs.Restaurant.Api.Controllers;

[ApiController]
public sealed class OrdersController(IOrderService orderService) : ControllerBase
{
    [HttpGet("api/restaurants/{restaurantId:guid}/orders")]
    public async Task<IActionResult> GetRestaurantOrders(Guid restaurantId, CancellationToken cancellationToken)
    {
        return Ok(await orderService.GetRestaurantOrdersAsync(restaurantId, cancellationToken));
    }

    [HttpGet("api/orders/{id:guid}")]
    public async Task<IActionResult> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderAsync(id, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }

    [HttpPut("api/orders/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        var order = await orderService.UpdateStatusAsync(id, request, cancellationToken);
        return order is null ? NotFound() : Ok(order);
    }
}
