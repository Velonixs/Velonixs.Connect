using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Velonixs.Connect.Api.Security;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Contracts.Common;
using Velonixs.Connect.Contracts.Orders;
using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Api.Controllers;

[ApiController]
[Authorize]
public sealed class OrdersController(
    IOrderService orderService,
    ApiBusinessAccessService access) : ControllerBase
{
    [HttpGet("api/restaurants/{restaurantId:guid}/orders")]
    [HttpGet("api/businesses/{restaurantId:guid}/orders")]
    [HttpGet("api/v1/restaurants/{restaurantId:guid}/orders")]
    [HttpGet("api/v1/businesses/{restaurantId:guid}/orders")]
    public async Task<IActionResult> GetRestaurantOrders(Guid restaurantId, CancellationToken cancellationToken)
    {
        if (!access.CanManageOrders(restaurantId))
        {
            return Forbid();
        }

        return Ok(await orderService.GetRestaurantOrdersAsync(restaurantId, cancellationToken));
    }

    [HttpGet("api/v1/orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] Guid? restaurantId,
        CancellationToken cancellationToken)
    {
        var effectiveRestaurantId = restaurantId ?? access.AssignedBusinessId;

        if (effectiveRestaurantId is not Guid businessId)
        {
            return BadRequest(new ApiErrorResponse(
                "restaurant_required",
                "A restaurantId query value is required for platform administrators.",
                HttpContext.TraceIdentifier));
        }

        if (!access.CanManageOrders(businessId))
        {
            return Forbid();
        }

        return Ok(await orderService.GetRestaurantOrdersAsync(businessId, cancellationToken));
    }

    [HttpGet("api/orders/{id:guid}")]
    [HttpGet("api/v1/orders/{id:guid}")]
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
    [HttpPut("api/v1/orders/{id:guid}/status")]
    [HttpPatch("api/v1/orders/{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!await access.CanReadOrderAsync(id, cancellationToken))
        {
            return Forbid();
        }

        OrderDetailResponse? order;
        try
        {
            order = await orderService.UpdateStatusAsync(id, request, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(
                "invalid_order_status_transition",
                ex.Message,
                HttpContext.TraceIdentifier));
        }

        return order is null ? NotFound() : Ok(order);
    }

    [HttpPost("api/v1/orders/{id:guid}/confirm")]
    public async Task<IActionResult> ConfirmOrder(
        Guid id,
        [FromBody] ConfirmOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!await access.CanReadOrderAsync(id, cancellationToken))
        {
            return Forbid();
        }

        return await UpdateOrderStatusFromCommand(
            id,
            new UpdateOrderStatusRequest(
                OrderStatuses.Confirmed,
                request.Comment,
                ResolveUpdatedBy(),
                request.EstimatedMinutes),
            cancellationToken);
    }

    [HttpPost("api/v1/orders/{id:guid}/reject")]
    public async Task<IActionResult> RejectOrder(
        Guid id,
        [FromBody] RejectOrderRequest request,
        CancellationToken cancellationToken)
    {
        if (!await access.CanReadOrderAsync(id, cancellationToken))
        {
            return Forbid();
        }

        return await UpdateOrderStatusFromCommand(
            id,
            new UpdateOrderStatusRequest(
                OrderStatuses.Rejected,
                request.Reason,
                ResolveUpdatedBy()),
            cancellationToken);
    }

    private async Task<IActionResult> UpdateOrderStatusFromCommand(
        Guid id,
        UpdateOrderStatusRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var order = await orderService.UpdateStatusAsync(id, request, cancellationToken);
            return order is null ? NotFound() : Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiErrorResponse(
                "invalid_order_status_transition",
                ex.Message,
                HttpContext.TraceIdentifier));
        }
    }

    private string ResolveUpdatedBy() =>
        User.Identity?.Name ??
        User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ??
        "API user";
}
