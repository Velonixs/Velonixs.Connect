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

public sealed class CustomerPortalSummary
{
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? LastAddress { get; set; }
    public DateTimeOffset LastInteractionAt { get; set; }
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
