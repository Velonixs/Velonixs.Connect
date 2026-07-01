using System.ComponentModel.DataAnnotations;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Admin.Models;

public sealed class AdminIndexViewModel
{
    public IReadOnlyCollection<RestaurantAdminSummary> Restaurants { get; set; } = Array.Empty<RestaurantAdminSummary>();
    public RestaurantFormModel NewRestaurant { get; set; } = new();
    public TaxSettingFormModel TaxSetting { get; set; } = new();
    public IReadOnlyCollection<string> BusinessTypeOptions { get; set; } = BusinessTypes.All;
    public int TotalRestaurants { get; set; }
    public int ActiveRestaurants { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
}

public sealed class TaxSettingFormModel
{
    [Range(0, 100)]
    public decimal CgstPercent { get; set; }

    [Range(0, 100)]
    public decimal SgstPercent { get; set; }
}

public sealed class MasterCatalogManageViewModel
{
    public MasterCatalogResponse? MasterCatalog { get; set; }
    public MasterCatalogCategoryFormModel NewMasterCategory { get; set; } = new();
    public MasterCatalogItemFormModel NewMasterItem { get; set; } = new();
}

public sealed class MasterCatalogCategoryFormModel
{
    [Required]
    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class MasterCatalogItemFormModel
{
    [Required]
    public Guid MasterCategoryId { get; set; }

    [Required]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class RestaurantAdminSummary
{
    public RestaurantResponse Restaurant { get; set; } = null!;
    public int MenuItemCount { get; set; }
    public int CustomerCount { get; set; }
    public int OrderCount { get; set; }
    public decimal Revenue { get; set; }
    public DateTimeOffset? LastOrderAt { get; set; }
}

public sealed class RestaurantManageViewModel
{
    public RestaurantResponse Restaurant { get; set; } = null!;
    public RestaurantFormModel RestaurantForm { get; set; } = new();
    public IReadOnlyCollection<string> BusinessTypeOptions { get; set; } = BusinessTypes.All;
    public MenuResponse? Menu { get; set; }
    public IReadOnlyCollection<OrderSummaryResponse> Orders { get; set; } = Array.Empty<OrderSummaryResponse>();
    public IReadOnlyCollection<CustomerAdminSummary> Customers { get; set; } = Array.Empty<CustomerAdminSummary>();
    public MenuCategoryFormModel NewCategory { get; set; } = new();
    public MenuItemFormModel NewItem { get; set; } = new();
    public int ActiveMenuItemCount { get; set; }
    public int PendingOrderCount { get; set; }
    public decimal Revenue { get; set; }
    public IReadOnlyCollection<BusinessUserSummary> Users { get; set; } = Array.Empty<BusinessUserSummary>();
    public OwnerAccountFormModel NewOwner { get; set; } = new();
}

public sealed class BusinessUserSummary
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class OwnerAccountFormModel
{
    [Required]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;
}

public sealed class ResetPortalUserPasswordFormModel
{
    [Required]
    [MinLength(8)]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class CustomerAdminSummary
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? LastAddress { get; set; }
    public int OrderCount { get; set; }
    public DateTimeOffset LastInteractionAt { get; set; }
}

public sealed class OrderAdminViewModel
{
    public OrderDetailResponse Order { get; set; } = null!;
    public RestaurantResponse? Restaurant { get; set; }
    public IReadOnlyCollection<string> StatusOptions { get; set; } = Array.Empty<string>();
}

public sealed class RestaurantFormModel
{
    [Required]
    public string Name { get; set; } = string.Empty;
    public string BusinessType { get; set; } = BusinessTypes.Restaurant;

    [Required]
    public string WhatsAppPhoneNumberId { get; set; } = string.Empty;
    public string? BusinessPhone { get; set; }
    public string? NotificationEmail { get; set; }
    public string? StaffWhatsAppNumber { get; set; }
    public string? WhatsAppCatalogId { get; set; }
    public string? Address { get; set; }
    [Range(0, 100)]
    public decimal CgstPercent { get; set; }
    [Range(0, 100)]
    public decimal SgstPercent { get; set; }
    public bool IsActive { get; set; } = true;

    [Required]
    public string OwnerName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string OwnerEmail { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    [DataType(DataType.Password)]
    public string OwnerPassword { get; set; } = string.Empty;
}

public sealed class MenuCategoryFormModel
{
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class MenuItemFormModel
{
    public Guid CategoryId { get; set; }
    public int ItemCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ProductRetailerId { get; set; }
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public sealed class OrderStatusFormModel
{
    public string Status { get; set; } = string.Empty;
    public int? EstimatedMinutes { get; set; }
    public int? CustomEstimatedMinutes { get; set; }
    public string? Comment { get; set; }

    public int? EffectiveEstimatedMinutes => CustomEstimatedMinutes is > 0
        ? CustomEstimatedMinutes
        : EstimatedMinutes;
}
