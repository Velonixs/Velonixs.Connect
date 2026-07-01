using System.ComponentModel.DataAnnotations;
using Velonixs.Connect.Application.Models;
using Velonixs.Connect.Shared.Security;

namespace Velonixs.Connect.Portal.Models;

public sealed class PortalDashboardViewModel
{
    public RestaurantResponse Restaurant { get; set; } = null!;
    public IReadOnlyCollection<OrderSummaryResponse> Orders { get; set; } = Array.Empty<OrderSummaryResponse>();
    public MenuResponse? Menu { get; set; }
    public int PendingOrderCount { get; set; }
    public int TodayOrderCount { get; set; }
    public decimal TodayRevenue { get; set; }
    public int AvailableItemCount { get; set; }
    public IReadOnlyCollection<CustomerPortalSummary> RecentCustomers { get; set; } = Array.Empty<CustomerPortalSummary>();
    public string CurrentRole { get; set; } = string.Empty;
}

public sealed class PortalMenuViewModel
{
    public RestaurantResponse Restaurant { get; set; } = null!;
    public MenuResponse? Menu { get; set; }
    public MasterCatalogResponse? MasterCatalog { get; set; }
    public MenuCategoryPortalFormModel NewCategory { get; set; } = new();
    public MenuItemPortalFormModel NewItem { get; set; } = new();
    public RestaurantTaxFormModel TaxSetting { get; set; } = new();
    public bool CanManageMenu { get; set; }
}

public sealed class RestaurantTaxFormModel
{
    [Range(0, 100)]
    public decimal CgstPercent { get; set; }

    [Range(0, 100)]
    public decimal SgstPercent { get; set; }
}

public sealed class PortalCustomersViewModel
{
    public RestaurantResponse Restaurant { get; set; } = null!;
    public IReadOnlyCollection<CustomerPortalSummary> Customers { get; set; } = Array.Empty<CustomerPortalSummary>();
}

public sealed class CustomerPortalSummary
{
    public Guid Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? LastAddress { get; set; }
    public DateTimeOffset LastInteractionAt { get; set; }
}

public sealed class CustomerEditFormModel
{
    public string? Name { get; set; }
    public string? LastAddress { get; set; }
}

public sealed class PortalOrderViewModel
{
    public RestaurantResponse Restaurant { get; set; } = null!;
    public OrderDetailResponse Order { get; set; } = null!;
    public IReadOnlyCollection<string> StatusOptions { get; set; } = Array.Empty<string>();
}

public sealed class PortalOrderStatusFormModel
{
    public string Status { get; set; } = string.Empty;
    public int? EstimatedMinutes { get; set; }
    public int? CustomEstimatedMinutes { get; set; }
    public string? Comment { get; set; }

    public int? EffectiveEstimatedMinutes => CustomEstimatedMinutes is > 0
        ? CustomEstimatedMinutes
        : EstimatedMinutes;
}

public sealed class MenuAvailabilityFormModel
{
    public bool IsAvailable { get; set; }
}

public sealed class MenuCategoryPortalFormModel
{
    [Required]
    public Guid MasterCategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class MenuItemPortalFormModel
{
    [Required]
    public Guid MasterMenuItemId { get; set; }

    [Required]
    public Guid CategoryId { get; set; }

    [Range(1, int.MaxValue)]
    public int ItemCode { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? ProductRetailerId { get; set; }

    [Range(0, 999999)]
    public decimal Price { get; set; }

    public bool IsAvailable { get; set; } = true;

    public bool IsActive { get; set; } = true;
}

public sealed class StaffIndexViewModel
{
    public RestaurantResponse Business { get; set; } = null!;
    public IReadOnlyCollection<StaffUserSummary> Users { get; set; } = Array.Empty<StaffUserSummary>();
    public CreateStaffUserModel NewUser { get; set; } = new();
    public IReadOnlyCollection<string> RoleOptions { get; set; } = AppRoles.StaffAssignable;
}

public sealed class StaffUserSummary
{
    public Guid Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public sealed class CreateStaffUserModel
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

    [Required]
    public string Role { get; set; } = AppRoles.Staff;
}
