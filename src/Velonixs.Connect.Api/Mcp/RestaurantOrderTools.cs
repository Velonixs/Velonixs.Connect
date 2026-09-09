using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.Mcp;

[McpServerToolType]
[Authorize(Roles = McpTenantResolver.AllTenantRoles)]
public sealed class RestaurantOrderTools(IOrderService orderService)
{
    [McpServerTool(Name = "restaurant_get_orders", ReadOnly = true, Destructive = false, UseStructuredContent = true)]
    [Description("Gets orders belonging to the authenticated restaurant tenant.")]
    public async Task<IReadOnlyCollection<OrderSummaryResponse>> GetOrdersAsync(
        ClaimsPrincipal? user,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(user);
        var orders = await orderService.GetRestaurantOrdersAsync(tenantId, cancellationToken);
        return string.IsNullOrWhiteSpace(status)
            ? orders
            : orders.Where(order => string.Equals(order.OrderStatus, status, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    [McpServerTool(Name = "restaurant_get_pending_orders", ReadOnly = true, Destructive = false, UseStructuredContent = true)]
    [Description("Gets pending orders belonging to the authenticated restaurant tenant.")]
    public Task<IReadOnlyCollection<OrderSummaryResponse>> GetPendingOrdersAsync(
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default) =>
        GetOrdersAsync(user, OrderStatuses.PendingConfirmation, cancellationToken);

    [McpServerTool(Name = "restaurant_get_today_orders", ReadOnly = true, Destructive = false, UseStructuredContent = true)]
    [Description("Gets UTC-today orders belonging to the authenticated restaurant tenant.")]
    public async Task<IReadOnlyCollection<OrderSummaryResponse>> GetTodayOrdersAsync(
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        var orders = await GetOrdersAsync(user, cancellationToken: cancellationToken);
        var today = DateTimeOffset.UtcNow.Date;
        return orders.Where(order => order.CreatedAt.UtcDateTime.Date == today).ToArray();
    }

    [McpServerTool(Name = "restaurant_get_order", ReadOnly = true, Destructive = false, UseStructuredContent = true)]
    [Description("Gets one order only when it belongs to the authenticated restaurant tenant.")]
    public async Task<OrderDetailResponse?> GetOrderAsync(
        ClaimsPrincipal? user,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(user);
        var order = await orderService.GetOrderAsync(orderId, cancellationToken);
        return order?.RestaurantId == tenantId ? order : null;
    }

    [McpServerTool(Name = "restaurant_accept_order", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Accepts a pending order for the authenticated restaurant and notifies the customer.")]
    public async Task<OrderDetailResponse> AcceptOrderAsync(
        ClaimsPrincipal? user,
        Guid orderId,
        int preparationTimeMinutes = 20,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(user);
        _ = await RequireTenantOrderAsync(tenantId, orderId, cancellationToken);
        return await orderService.UpdateStatusAsync(
                   orderId,
                   new UpdateOrderStatusRequest(
                       OrderStatuses.Confirmed,
                       "Order accepted.",
                       user?.Identity?.Name ?? "MCP",
                       preparationTimeMinutes),
                   cancellationToken)
               ?? throw new KeyNotFoundException("Order was not found.");
    }

    [McpServerTool(Name = "restaurant_reject_order", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true)]
    [Description("Rejects a pending order for the authenticated restaurant with a customer-visible reason.")]
    public async Task<OrderDetailResponse> RejectOrderAsync(
        ClaimsPrincipal? user,
        Guid orderId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(user);
        _ = await RequireTenantOrderAsync(tenantId, orderId, cancellationToken);
        return await orderService.UpdateStatusAsync(
                   orderId,
                   new UpdateOrderStatusRequest(
                       OrderStatuses.Rejected,
                       reason,
                       user?.Identity?.Name ?? "MCP"),
                   cancellationToken)
               ?? throw new KeyNotFoundException("Order was not found.");
    }

    [McpServerTool(Name = "restaurant_set_preparation_time", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Description("Sets preparation time for an accepted order belonging to the authenticated restaurant.")]
    public async Task<OrderDetailResponse> SetPreparationTimeAsync(
        ClaimsPrincipal? user,
        Guid orderId,
        int preparationTimeMinutes,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(user);
        _ = await RequireTenantOrderAsync(tenantId, orderId, cancellationToken);
        return await orderService.SetPreparationTimeAsync(
                   orderId,
                   preparationTimeMinutes,
                   user?.Identity?.Name ?? "MCP",
                   cancellationToken)
               ?? throw new KeyNotFoundException("Order was not found.");
    }

    private static Guid ResolveTenant(ClaimsPrincipal? user) =>
        McpTenantResolver.Resolve(
            user,
            AppRoles.PlatformAdmin,
            AppRoles.BusinessOwner,
            AppRoles.BusinessManager,
            AppRoles.Cashier,
            AppRoles.Staff);

    private async Task<OrderDetailResponse> RequireTenantOrderAsync(
        Guid tenantId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await orderService.GetOrderAsync(orderId, cancellationToken);
        return order is { RestaurantId: var restaurantId } && restaurantId == tenantId
            ? order
            : throw new KeyNotFoundException("Order was not found in the authenticated restaurant.");
    }
}
