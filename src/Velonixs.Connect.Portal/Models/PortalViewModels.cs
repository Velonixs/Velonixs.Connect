using Velonixs.Connect.Application.Models;

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
}

public sealed class MenuAvailabilityFormModel
{
    public bool IsAvailable { get; set; }
}
