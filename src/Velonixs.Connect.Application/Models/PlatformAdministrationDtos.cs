namespace Velonixs.Connect.Application.Models;

/// <summary>
/// Read model and commands used by the platform administration boundary. UI
/// hosts consume these contracts instead of EF Core or ASP.NET Identity types.
/// </summary>
public sealed record PlatformDashboardResponse(
    IReadOnlyCollection<PlatformBusinessSummary> Businesses,
    int ActiveProductCount,
    int PendingCatalogSyncCount,
    int TodaysOrderCount,
    decimal TotalRevenue);

public sealed record PlatformBusinessSummary(
    Guid BusinessId,
    string Name,
    string BusinessType,
    bool IsActive,
    int ActiveProductCount,
    int OrderCount);

public sealed record CreateBusinessWithOwnerRequest(
    string Name,
    string? BusinessType,
    string WhatsAppPhoneNumberId,
    string? BusinessPhone,
    string? NotificationEmail,
    string? StaffWhatsAppNumber,
    string? Address,
    decimal CgstPercent,
    decimal SgstPercent,
    bool IsActive,
    string? WhatsAppCatalogId,
    string OwnerDisplayName,
    string OwnerEmail,
    string OwnerPassword);

public sealed record BusinessUserResponse(
    Guid Id,
    Guid BusinessId,
    string DisplayName,
    string Email,
    string Role,
    bool IsActive);

public sealed record CreateBusinessOwnerRequest(
    string DisplayName,
    string Email,
    string Password);

public sealed record CustomerSummaryResponse(
    Guid Id,
    string? Name,
    string PhoneNumber,
    string? LastAddress,
    int OrderCount,
    DateTimeOffset LastInteractionAt);
