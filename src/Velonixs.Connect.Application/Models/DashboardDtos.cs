namespace Velonixs.Connect.Application.Models;

public sealed record RestaurantDashboardResponse(
    RestaurantResponse Restaurant,
    IReadOnlyCollection<OrderSummaryResponse> RecentOrders,
    MenuResponse? Menu,
    int PendingOrderCount,
    int TodayOrderCount,
    decimal TodayRevenue,
    int AvailableItemCount,
    IReadOnlyCollection<CustomerSummaryResponse> RecentCustomers);
