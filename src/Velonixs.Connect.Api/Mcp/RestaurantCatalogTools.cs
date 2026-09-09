using System.ComponentModel;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Server;
using Velonixs.Connect.Application.Abstractions;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Api.Mcp;

[McpServerToolType]
[Authorize(Roles = McpTenantResolver.AllTenantRoles)]
public sealed class RestaurantCatalogTools(
    IMenuService menuService,
    IMetaCatalogService metaCatalogService,
    IWhatsAppCatalogService whatsAppCatalogService,
    IWhatsAppCommerceService commerceService)
{
    [McpServerTool(Name = "restaurant_get_catalog", ReadOnly = true, Destructive = false, UseStructuredContent = true)]
    [Description("Gets the authenticated restaurant's Meta catalog overview without returning credentials.")]
    public Task<MetaCatalogOverview?> GetCatalogAsync(
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default) =>
        metaCatalogService.GetCatalogAsync(ResolveTenant(user), cancellationToken);

    [McpServerTool(Name = "restaurant_get_catalog_status", ReadOnly = true, Destructive = false, UseStructuredContent = true)]
    [Description("Gets synchronization counts for the authenticated restaurant catalog.")]
    public Task<MetaCatalogSyncStatusResponse> GetCatalogStatusAsync(
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default) =>
        metaCatalogService.GetCatalogSyncStatusAsync(ResolveTenant(user), cancellationToken);

    [McpServerTool(Name = "restaurant_get_menu", ReadOnly = true, Destructive = false, UseStructuredContent = true)]
    [Description("Gets the authenticated restaurant's authoritative local menu.")]
    public Task<MenuResponse?> GetMenuAsync(
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default) =>
        menuService.GetMenuAsync(ResolveTenant(user), cancellationToken);

    [McpServerTool(Name = "restaurant_get_menu_item", ReadOnly = true, Destructive = false, UseStructuredContent = true)]
    [Description("Gets one menu item only when it belongs to the authenticated restaurant.")]
    public async Task<MenuItemResponse?> GetMenuItemAsync(
        ClaimsPrincipal? user,
        [Description("Menu item ID.")] Guid menuItemId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveTenant(user);
        return await FindTenantItemAsync(tenantId, menuItemId, cancellationToken);
    }

    [McpServerTool(Name = "restaurant_sync_catalog", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Authorize(Roles = McpTenantResolver.CatalogManagerRoles)]
    [Description("Queues the authenticated restaurant's complete local menu for Meta catalog synchronization.")]
    public async Task<object> SyncCatalogAsync(
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveCatalogManager(user);
        var queued = await metaCatalogService.SyncRestaurantCatalogAsync(tenantId, cancellationToken);
        return new { restaurantId = tenantId, queued };
    }

    [McpServerTool(Name = "restaurant_add_menu_item", ReadOnly = false, Destructive = false, UseStructuredContent = true)]
    [Authorize(Roles = McpTenantResolver.CatalogManagerRoles)]
    [Description("Adds an item to the authenticated restaurant's local menu and queues catalog synchronization.")]
    public Task<MenuItemResponse> AddMenuItemAsync(
        ClaimsPrincipal? user,
        [Description("Existing category ID in this restaurant.")] Guid categoryId,
        [Description("Unique positive item code in this restaurant.")] int itemCode,
        string name,
        decimal price,
        string? description = null,
        string? productRetailerId = null,
        string? imageUrl = null,
        bool isVegetarian = false,
        int? preparationTimeMinutes = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveCatalogManager(user);
        return menuService.CreateItemAsync(
            tenantId,
            new CreateMenuItemRequest(
                categoryId,
                null,
                itemCode,
                name,
                description,
                price,
                true,
                true,
                productRetailerId,
                imageUrl,
                Currency: "INR",
                IsVegetarian: isVegetarian,
                PreparationTimeMinutes: preparationTimeMinutes),
            cancellationToken);
    }

    [McpServerTool(Name = "restaurant_update_menu_item", ReadOnly = false, Destructive = false, UseStructuredContent = true)]
    [Authorize(Roles = McpTenantResolver.CatalogManagerRoles)]
    [Description("Updates an item in the authenticated restaurant and queues catalog synchronization.")]
    public async Task<MenuItemResponse> UpdateMenuItemAsync(
        ClaimsPrincipal? user,
        Guid menuItemId,
        Guid categoryId,
        int itemCode,
        string name,
        decimal price,
        bool isAvailable,
        bool isActive,
        string? description = null,
        string? imageUrl = null,
        bool isVegetarian = false,
        int? preparationTimeMinutes = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveCatalogManager(user);
        var current = await RequireTenantItemAsync(tenantId, menuItemId, cancellationToken);
        return await menuService.UpdateItemAsync(
                   menuItemId,
                   new UpdateMenuItemRequest(
                       categoryId,
                       current.MasterMenuItemId,
                       itemCode,
                       name,
                       description,
                       price,
                       isAvailable,
                       isActive,
                       current.ProductRetailerId,
                       imageUrl,
                       current.DiscountPrice,
                       current.Currency,
                       isVegetarian,
                       preparationTimeMinutes),
                   cancellationToken)
               ?? throw new KeyNotFoundException("Menu item was not found.");
    }

    [McpServerTool(Name = "restaurant_update_price", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Authorize(Roles = McpTenantResolver.CatalogManagerRoles)]
    [Description("Changes a menu item's authoritative price and queues Meta synchronization.")]
    public Task<MenuItemResponse> UpdatePriceAsync(
        ClaimsPrincipal? user,
        Guid menuItemId,
        decimal price,
        CancellationToken cancellationToken = default) =>
        UpdateCurrentItemAsync(ResolveCatalogManager(user), menuItemId, price: price, cancellationToken: cancellationToken);

    [McpServerTool(Name = "restaurant_set_item_available", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Authorize(Roles = McpTenantResolver.CatalogManagerRoles)]
    [Description("Marks an authenticated restaurant menu item available and queues Meta synchronization.")]
    public Task<MenuItemResponse> SetAvailableAsync(
        ClaimsPrincipal? user,
        Guid menuItemId,
        CancellationToken cancellationToken = default) =>
        SetAvailabilityAsync(ResolveCatalogManager(user), menuItemId, true, cancellationToken);

    [McpServerTool(Name = "restaurant_set_item_sold_out", ReadOnly = false, Destructive = false, Idempotent = true, UseStructuredContent = true)]
    [Authorize(Roles = McpTenantResolver.CatalogManagerRoles)]
    [Description("Marks an authenticated restaurant menu item sold out and queues Meta synchronization.")]
    public Task<MenuItemResponse> SetSoldOutAsync(
        ClaimsPrincipal? user,
        Guid menuItemId,
        CancellationToken cancellationToken = default) =>
        SetAvailabilityAsync(ResolveCatalogManager(user), menuItemId, false, cancellationToken);

    [McpServerTool(Name = "restaurant_remove_menu_item", ReadOnly = false, Destructive = true, Idempotent = true, UseStructuredContent = true)]
    [Authorize(Roles = McpTenantResolver.CatalogManagerRoles)]
    [Description("Deactivates an authenticated restaurant menu item and queues its removal from Meta.")]
    public async Task<object> RemoveMenuItemAsync(
        ClaimsPrincipal? user,
        Guid menuItemId,
        CancellationToken cancellationToken = default)
    {
        var tenantId = ResolveCatalogManager(user);
        _ = await RequireTenantItemAsync(tenantId, menuItemId, cancellationToken);
        var removed = await menuService.DeactivateItemAsync(menuItemId, cancellationToken);
        return new { restaurantId = tenantId, menuItemId, removed };
    }

    [McpServerTool(Name = "restaurant_send_catalog", ReadOnly = false, Destructive = false, UseStructuredContent = true)]
    [Authorize(Roles = McpTenantResolver.CatalogManagerRoles)]
    [Description("Sends the native WhatsApp View Catalog message for the authenticated restaurant.")]
    public Task<CatalogMessageSendResult> SendCatalogAsync(
        ClaimsPrincipal? user,
        [Description("Customer WhatsApp number including country code.")] string customerPhoneNumber,
        bool isTest = false,
        CancellationToken cancellationToken = default) =>
        whatsAppCatalogService.SendCatalogMessageAsync(
            ResolveCatalogManager(user),
            customerPhoneNumber,
            isTest,
            cancellationToken);

    [McpServerTool(Name = "restaurant_validate_catalog", ReadOnly = true, Destructive = false, UseStructuredContent = true)]
    [Description("Validates the authenticated restaurant's catalog visibility, cart, credential, and phone connection.")]
    public Task<WhatsAppCommerceDiagnostics> ValidateCatalogAsync(
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default) =>
        commerceService.ValidateCatalogConnectionAsync(ResolveTenant(user), cancellationToken);

    private static Guid ResolveTenant(ClaimsPrincipal? user) =>
        McpTenantResolver.Resolve(
            user,
            AppRoles.PlatformAdmin,
            AppRoles.BusinessOwner,
            AppRoles.BusinessManager,
            AppRoles.Cashier,
            AppRoles.Staff);

    private static Guid ResolveCatalogManager(ClaimsPrincipal? user) =>
        McpTenantResolver.Resolve(user, AppRoles.PlatformAdmin, AppRoles.BusinessOwner, AppRoles.BusinessManager);

    private async Task<MenuItemResponse?> FindTenantItemAsync(
        Guid tenantId,
        Guid menuItemId,
        CancellationToken cancellationToken) =>
        (await menuService.GetMenuAsync(tenantId, cancellationToken))?.Items
        .FirstOrDefault(item => item.Id == menuItemId);

    private async Task<MenuItemResponse> RequireTenantItemAsync(
        Guid tenantId,
        Guid menuItemId,
        CancellationToken cancellationToken) =>
        await FindTenantItemAsync(tenantId, menuItemId, cancellationToken)
        ?? throw new KeyNotFoundException("Menu item was not found in the authenticated restaurant.");

    private async Task<MenuItemResponse> SetAvailabilityAsync(
        Guid tenantId,
        Guid menuItemId,
        bool isAvailable,
        CancellationToken cancellationToken)
    {
        _ = await RequireTenantItemAsync(tenantId, menuItemId, cancellationToken);
        return await menuService.SetItemAvailabilityAsync(menuItemId, isAvailable, cancellationToken)
               ?? throw new KeyNotFoundException("Menu item was not found.");
    }

    private async Task<MenuItemResponse> UpdateCurrentItemAsync(
        Guid tenantId,
        Guid menuItemId,
        decimal price,
        CancellationToken cancellationToken)
    {
        var current = await RequireTenantItemAsync(tenantId, menuItemId, cancellationToken);
        return await menuService.UpdateItemAsync(
                   menuItemId,
                   new UpdateMenuItemRequest(
                       current.CategoryId,
                       current.MasterMenuItemId,
                       current.ItemCode,
                       current.Name,
                       current.Description,
                       price,
                       current.IsAvailable,
                       current.IsActive,
                       current.ProductRetailerId,
                       current.ImageUrl,
                       current.DiscountPrice,
                       current.Currency,
                       current.IsVegetarian,
                       current.PreparationTimeMinutes),
                   cancellationToken)
               ?? throw new KeyNotFoundException("Menu item was not found.");
    }
}
