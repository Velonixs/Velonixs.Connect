using Microsoft.EntityFrameworkCore;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;
using Velonixs.Connect.Infrastructure.Configuration;
using Velonixs.Connect.Infrastructure.Services;
using Velonixs.Connect.Persistence.Persistence;
using Xunit;

namespace Velonixs.Connect.Infrastructure.Tests;

public sealed class MenuServiceMetaCatalogBehaviorTests
{
    [Fact]
    public async Task GetMenuAsync_ReturnsSortedBusinessMenuAndNullForAnUnknownBusiness()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var sync = new RecordingMetaCatalogSyncService();
        var service = new MenuService(dbContext, sync);

        Assert.Null(await service.GetMenuAsync(Guid.NewGuid()));

        var restaurant = await AddRestaurantAsync(dbContext, "Sorted Bistro");
        var pizza = new MenuCategory { RestaurantId = restaurant.Id, Name = "Pizza", DisplayOrder = 2 };
        var starters = new MenuCategory { RestaurantId = restaurant.Id, Name = "Starters", DisplayOrder = 1 };
        dbContext.MenuCategories.AddRange(pizza, starters);
        dbContext.MenuItems.AddRange(
            new MenuItem
            {
                RestaurantId = restaurant.Id,
                Category = pizza,
                ItemCode = 20,
                Name = "Second",
                Price = 200
            },
            new MenuItem
            {
                RestaurantId = restaurant.Id,
                Category = starters,
                ItemCode = 10,
                Name = "First",
                Price = 100
            });
        await dbContext.SaveChangesAsync();

        var menu = await service.GetMenuAsync(restaurant.Id);

        Assert.NotNull(menu);
        Assert.Equal("Sorted Bistro", menu!.BusinessName);
        Assert.Equal(new[] { "Starters", "Pizza" }, menu.Categories.Select(x => x.Name));
        Assert.Equal(new[] { "First", "Second" }, menu.Items.Select(x => x.Name));
        Assert.Equal("Starters", menu.Items.First().CategoryName);
    }

    [Fact]
    public async Task MasterCatalog_CreateAndUpdateOperationsNormalizeNamesAndGuardDuplicates()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var service = new MenuService(dbContext, new RecordingMetaCatalogSyncService());

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateMasterCategoryAsync(
            new CreateMasterMenuCategoryRequest("  ")));
        var drinks = await service.CreateMasterCategoryAsync(
            new CreateMasterMenuCategoryRequest(" Drinks ", DisplayOrder: 2));
        var pizza = await service.CreateMasterCategoryAsync(
            new CreateMasterMenuCategoryRequest("Pizza", DisplayOrder: 1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateMasterCategoryAsync(
            new CreateMasterMenuCategoryRequest("Drinks")));

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateMasterItemAsync(
            new CreateMasterMenuItemRequest(drinks.Id, " ", null)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateMasterItemAsync(
            new CreateMasterMenuItemRequest(Guid.NewGuid(), "Tea", null)));
        var tea = await service.CreateMasterItemAsync(
            new CreateMasterMenuItemRequest(drinks.Id, " Tea ", " Hot "));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateMasterItemAsync(
            new CreateMasterMenuItemRequest(drinks.Id, "Tea", null)));

        Assert.Null(await service.UpdateMasterCategoryAsync(
            Guid.NewGuid(),
            new UpdateMasterMenuCategoryRequest("Unused", 0, true)));
        var updatedCategory = await service.UpdateMasterCategoryAsync(
            drinks.Id,
            new UpdateMasterMenuCategoryRequest("Beverages", 3, false));
        Assert.NotNull(updatedCategory);
        Assert.Equal("Beverages", updatedCategory!.Name);
        Assert.False(updatedCategory.IsActive);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateMasterCategoryAsync(
            drinks.Id,
            new UpdateMasterMenuCategoryRequest("Pizza", 0, true)));

        Assert.Null(await service.UpdateMasterItemAsync(
            Guid.NewGuid(),
            new UpdateMasterMenuItemRequest(pizza.Id, "Unknown", null, true)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateMasterItemAsync(
            tea.Id,
            new UpdateMasterMenuItemRequest(Guid.NewGuid(), "Tea", null, true)));
        var updatedTea = await service.UpdateMasterItemAsync(
            tea.Id,
            new UpdateMasterMenuItemRequest(pizza.Id, " Masala tea ", "  ", false));
        Assert.NotNull(updatedTea);
        Assert.Equal(pizza.Id, updatedTea!.MasterCategoryId);
        Assert.Equal("Masala tea", updatedTea.Name);
        Assert.Null(updatedTea.Description);
        Assert.False(updatedTea.IsActive);

        var cola = await service.CreateMasterItemAsync(
            new CreateMasterMenuItemRequest(pizza.Id, "Cola", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateMasterItemAsync(
            cola.Id,
            new UpdateMasterMenuItemRequest(pizza.Id, "Masala tea", null, true)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateMasterItemAsync(
            cola.Id,
            new UpdateMasterMenuItemRequest(Guid.Empty, "Cola", null, true)));

        var catalog = await service.GetMasterCatalogAsync();
        Assert.Equal(new[] { "Pizza", "Beverages" }, catalog.Categories.Select(x => x.Name));
        Assert.Equal(new[] { "Cola", "Masala tea" }, catalog.Items.Select(x => x.Name));
    }

    [Fact]
    public async Task MasterCatalog_DeleteOperationsProtectReferencedRecords()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var service = new MenuService(dbContext, new RecordingMetaCatalogSyncService());

        Assert.False(await service.DeleteMasterCategoryAsync(Guid.NewGuid()));
        var disposableCategory = await service.CreateMasterCategoryAsync(
            new CreateMasterMenuCategoryRequest("Disposable"));
        Assert.True(await service.DeleteMasterCategoryAsync(disposableCategory.Id));

        var itemCategory = await service.CreateMasterCategoryAsync(
            new CreateMasterMenuCategoryRequest("Item category"));
        var referencedItem = await service.CreateMasterItemAsync(
            new CreateMasterMenuItemRequest(itemCategory.Id, "Referenced item", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteMasterCategoryAsync(itemCategory.Id));

        var mappedCategory = await service.CreateMasterCategoryAsync(
            new CreateMasterMenuCategoryRequest("Mapped category"));
        var restaurant = await AddRestaurantAsync(dbContext, "Mapped Bistro");
        await service.CreateCategoryAsync(
            restaurant.Id,
            new CreateMenuCategoryRequest("ignored", MasterCategoryId: mappedCategory.Id));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteMasterCategoryAsync(mappedCategory.Id));

        Assert.False(await service.DeleteMasterItemAsync(Guid.NewGuid()));
        var disposableItem = await service.CreateMasterItemAsync(
            new CreateMasterMenuItemRequest(itemCategory.Id, "Disposable item", null));
        Assert.True(await service.DeleteMasterItemAsync(disposableItem.Id));

        var businessCategory = await service.CreateCategoryAsync(
            restaurant.Id,
            new CreateMenuCategoryRequest("Products"));
        await service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(
                businessCategory.Id,
                referencedItem.Id,
                1,
                "ignored",
                null,
                10));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteMasterItemAsync(referencedItem.Id));
    }

    [Fact]
    public async Task CategoryOperationsValidateMasterMappingsAndSupportTheLifecycle()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var service = new MenuService(dbContext, new RecordingMetaCatalogSyncService());
        var restaurant = await AddRestaurantAsync(dbContext, "Category Bistro");

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCategoryAsync(
            Guid.Empty,
            new CreateMenuCategoryRequest("Pizza")));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateCategoryAsync(
            restaurant.Id,
            new CreateMenuCategoryRequest("  ")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCategoryAsync(
            restaurant.Id,
            new CreateMenuCategoryRequest("Pizza", MasterCategoryId: Guid.NewGuid())));

        var master = await service.CreateMasterCategoryAsync(new CreateMasterMenuCategoryRequest("Master pizza"));
        var category = await service.CreateCategoryAsync(
            restaurant.Id,
            new CreateMenuCategoryRequest("ignored", DisplayOrder: 3, MasterCategoryId: master.Id));
        Assert.Equal("Master pizza", category.Name);
        Assert.Equal(master.Id, category.MasterCategoryId);
        Assert.Null(await service.UpdateCategoryAsync(
            Guid.NewGuid(),
            new UpdateMenuCategoryRequest("Unknown", 0, true)));

        var updated = await service.UpdateCategoryAsync(
            category.Id,
            new UpdateMenuCategoryRequest("Pizzas", 5, false));
        Assert.NotNull(updated);
        Assert.Equal("Pizzas", updated!.Name);
        Assert.False(updated.IsActive);
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateCategoryAsync(
            category.Id,
            new UpdateMenuCategoryRequest(" ", 0, true)));

        Assert.False(await service.DeactivateCategoryAsync(Guid.NewGuid()));
        Assert.True(await service.DeactivateCategoryAsync(category.Id));
        var emptyCategory = await service.CreateCategoryAsync(restaurant.Id, new CreateMenuCategoryRequest("Empty"));
        Assert.True(await service.DeleteCategoryAsync(emptyCategory.Id));
        Assert.False(await service.DeleteCategoryAsync(Guid.NewGuid()));

        var populatedCategory = await service.CreateCategoryAsync(restaurant.Id, new CreateMenuCategoryRequest("Populated"));
        await service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(populatedCategory.Id, null, 1, "Item", null, 10));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteCategoryAsync(populatedCategory.Id));
    }

    [Fact]
    public async Task CategoryChangesQueueActiveProductsForCatalogReconciliation()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var sync = new RecordingMetaCatalogSyncService();
        var service = new MenuService(dbContext, sync);
        var restaurant = await AddRestaurantAsync(dbContext, "Catalog category Bistro");
        var category = await service.CreateCategoryAsync(restaurant.Id, new CreateMenuCategoryRequest("Meals"));
        await service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(category.Id, null, 1, "Curry", null, 120));
        sync.QueueCalls.Clear();

        await service.UpdateCategoryAsync(category.Id, new UpdateMenuCategoryRequest("Main meals", 2, true));
        await service.DeactivateCategoryAsync(category.Id);

        Assert.Equal(new[] { "update", "update" }, sync.QueueCalls.Select(call => call.EventType));
        Assert.All(sync.QueueCalls, call => Assert.Equal(restaurant.Id, call.BusinessId));
    }

    [Fact]
    public async Task MenuItemOperations_GenerateAndProtectSkusAndQueueEachCatalogChange()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var sync = new RecordingMetaCatalogSyncService();
        var service = new MenuService(dbContext, sync);
        var restaurant = await AddRestaurantAsync(dbContext, "Products Bistro");
        var category = await service.CreateCategoryAsync(restaurant.Id, new CreateMenuCategoryRequest("Products"));
        var secondaryCategory = await service.CreateCategoryAsync(restaurant.Id, new CreateMenuCategoryRequest("Other"));

        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItemAsync(
            Guid.Empty,
            new CreateMenuItemRequest(category.Id, null, 1, "Item", null, 10)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(Guid.Empty, null, 1, "Item", null, 10)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(category.Id, null, 0, "Item", null, 10)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(category.Id, null, 1, " ", null, 10)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(category.Id, null, 1, "Item", null, -1)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(category.Id, Guid.NewGuid(), 1, "Item", null, 10)));

        var generated = await service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(
                category.Id,
                null,
                1,
                " Generated item ",
                " Description ",
                120,
                ProductRetailerId: " ",
                ImageUrl: " https://images.example.test/item.jpg "));
        Assert.Equal($"vxc-{restaurant.Id:N}-{generated.Id:N}", generated.ProductRetailerId);
        Assert.Equal("Generated item", generated.Name);
        Assert.Equal("Description", generated.Description);
        Assert.Equal("https://images.example.test/item.jpg", generated.ImageUrl);

        var explicitSku = await service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(category.Id, null, 2, "Explicit item", null, 130, ProductRetailerId: "sku-2"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(category.Id, null, 3, "Duplicate SKU", null, 140, ProductRetailerId: "sku-2")));

        Assert.Null(await service.UpdateItemAsync(
            Guid.NewGuid(),
            new UpdateMenuItemRequest(category.Id, null, 1, "Missing", null, 1, true, true)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateItemAsync(
            generated.Id,
            new UpdateMenuItemRequest(Guid.NewGuid(), null, 1, "Generated", null, 1, true, true)));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateItemAsync(
            generated.Id,
            new UpdateMenuItemRequest(category.Id, null, 1, "Generated", null, 1, true, true, "sku-2")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateItemAsync(
            generated.Id,
            new UpdateMenuItemRequest(category.Id, null, 1, "Generated", null, 1, true, true, "new-sku")));

        var updated = await service.UpdateItemAsync(
            generated.Id,
            new UpdateMenuItemRequest(
                secondaryCategory.Id,
                null,
                11,
                " Updated ",
                " Updated description ",
                150,
                false,
                true,
                ProductRetailerId: " ",
                ImageUrl: " "));
        Assert.NotNull(updated);
        Assert.Equal(generated.ProductRetailerId, updated!.ProductRetailerId);
        Assert.Equal(secondaryCategory.Id, updated.CategoryId);
        Assert.Equal("Updated", updated.Name);
        Assert.Null(updated.ImageUrl);
        Assert.False(updated.IsAvailable);

        var masterCategory = await service.CreateMasterCategoryAsync(new CreateMasterMenuCategoryRequest("Master"));
        var masterItem = await service.CreateMasterItemAsync(
            new CreateMasterMenuItemRequest(masterCategory.Id, "Master product", "Master description"));
        var mapped = await service.UpdateItemAsync(
            generated.Id,
            new UpdateMenuItemRequest(
                secondaryCategory.Id,
                masterItem.Id,
                12,
                "ignored",
                "ignored",
                160,
                true,
                true));
        Assert.NotNull(mapped);
        Assert.Equal("Master product", mapped!.Name);
        Assert.Equal("Master description", mapped.Description);

        await service.UpdateMasterItemAsync(
            masterItem.Id,
            new UpdateMasterMenuItemRequest(masterCategory.Id, "Master product", "Master description", false));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UpdateItemAsync(
            generated.Id,
            new UpdateMenuItemRequest(secondaryCategory.Id, masterItem.Id, 12, "ignored", null, 160, true, true)));

        Assert.False(await service.DeactivateItemAsync(Guid.NewGuid()));
        Assert.True(await service.DeactivateItemAsync(generated.Id));
        var stored = await dbContext.MenuItems.AsNoTracking().SingleAsync(x => x.Id == generated.Id);
        Assert.False(stored.IsActive);
        Assert.False(stored.IsAvailable);
        Assert.Equal(
            new[] { "create", "create", "update", "update", "delete" },
            sync.QueueCalls.Select(x => x.EventType));
        Assert.All(sync.QueueCalls, call => Assert.Equal(restaurant.Id, call.BusinessId));
        Assert.Equal(explicitSku.Id, sync.QueueCalls[1].ProductId);
    }

    [Fact]
    public async Task CreateItemAsync_QueuesDeleteInsteadOfCreate_WhenInitiallyInactive()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var sync = new RecordingMetaCatalogSyncService();
        var service = new MenuService(dbContext, sync);
        var restaurant = await AddRestaurantAsync(dbContext, "Inactive product Bistro");
        var category = await service.CreateCategoryAsync(
            restaurant.Id,
            new CreateMenuCategoryRequest("Hidden items"));

        var item = await service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(
                category.Id,
                null,
                1,
                "Seasonal item",
                null,
                120,
                IsAvailable: false,
                IsActive: false));

        Assert.False(item.IsActive);
        var queueCall = Assert.Single(sync.QueueCalls);
        Assert.Equal(restaurant.Id, queueCall.BusinessId);
        Assert.Equal(item.Id, queueCall.ProductId);
        Assert.Equal("delete", queueCall.EventType);
        Assert.DoesNotContain(sync.QueueCalls, call => call.EventType == "create");
    }

    [Fact]
    public async Task SetItemAvailabilityAsync_QueuesAnUpdateForTheChangedProduct()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var sync = new RecordingMetaCatalogSyncService();
        var service = new MenuService(dbContext, sync);
        var restaurant = await AddRestaurantAsync(dbContext, "Availability Bistro");
        var category = await service.CreateCategoryAsync(
            restaurant.Id,
            new CreateMenuCategoryRequest("Menu"));
        var item = await service.CreateItemAsync(
            restaurant.Id,
            new CreateMenuItemRequest(category.Id, null, 1, "Tea", null, 50));
        sync.QueueCalls.Clear();

        var updated = await service.SetItemAvailabilityAsync(item.Id, false);

        Assert.NotNull(updated);
        Assert.False(updated!.IsAvailable);
        var stored = await dbContext.MenuItems.AsNoTracking().SingleAsync(x => x.Id == item.Id);
        Assert.False(stored.IsAvailable);
        var queueCall = Assert.Single(sync.QueueCalls);
        Assert.Equal(restaurant.Id, queueCall.BusinessId);
        Assert.Equal(item.Id, queueCall.ProductId);
        Assert.Equal("update", queueCall.EventType);
    }

    [Fact]
    public async Task DeleteItemAsync_QueuesDeleteSnapshotBeforePhysicallyRemovingTheProduct()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(
            dbContext,
            retailerId: "removed-product-sku");
        item.MetaProductId = "meta-product-to-delete";
        await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        await dbContext.SaveChangesAsync();

        var handler = new RecordingHttpMessageHandler();
        var sync = MetaCatalogTestSupport.CreateSyncService(
            dbContext,
            handler,
            new MetaCatalogOptions { DisableSending = false });
        var service = new MenuService(dbContext, sync);

        Assert.True(await service.DeleteItemAsync(item.Id));
        Assert.Null(await dbContext.MenuItems.AsNoTracking().FirstOrDefaultAsync(x => x.Id == item.Id));

        var queuedDelete = Assert.Single(await dbContext.CatalogSyncQueue.AsNoTracking().ToArrayAsync());
        Assert.Equal("delete", queuedDelete.EventType);
        Assert.Equal("removed-product-sku", queuedDelete.ProductRetailerId);
        Assert.Contains("\"metaProductId\":\"meta-product-to-delete\"", queuedDelete.PayloadJson);

        await sync.ProcessPendingAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.EndsWith("/meta-product-to-delete", request.Uri.AbsoluteUri);
    }

    [Fact]
    public async Task DeleteItemAsync_PreservesTheProductWhenCatalogDeleteCannotBeQueued()
    {
        await using var dbContext = MetaCatalogTestSupport.CreateDbContext();
        var (restaurant, _, item) = await MetaCatalogTestSupport.SeedProductAsync(
            dbContext,
            retailerId: "disabled-delete-sku");
        item.MetaProductId = "meta-product-to-preserve";
        var setting = await MetaCatalogTestSupport.EnableCatalogAsync(dbContext, restaurant.Id);
        setting.IsEnabled = false;
        await dbContext.SaveChangesAsync();

        var service = new MenuService(
            dbContext,
            MetaCatalogTestSupport.CreateSyncService(dbContext));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => service.DeleteItemAsync(item.Id));

        Assert.Contains("catalog synchronization", exception.Message);
        Assert.Contains("deactivate", exception.Message, StringComparison.OrdinalIgnoreCase);
        var stored = await dbContext.MenuItems.AsNoTracking().SingleAsync(menuItem => menuItem.Id == item.Id);
        Assert.Equal("meta-product-to-preserve", stored.MetaProductId);
        Assert.Empty(await dbContext.CatalogSyncQueue.AsNoTracking().ToArrayAsync());
    }

    private static async Task<Restaurant> AddRestaurantAsync(RestaurantConnectDbContext dbContext, string name)
    {
        var restaurant = new Restaurant
        {
            Name = name,
            WhatsAppPhoneNumberId = $"phone-{Guid.NewGuid():N}"
        };
        dbContext.Restaurants.Add(restaurant);
        await dbContext.SaveChangesAsync();
        return restaurant;
    }
}
