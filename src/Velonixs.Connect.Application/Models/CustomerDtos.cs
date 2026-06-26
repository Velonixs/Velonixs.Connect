using System.ComponentModel.DataAnnotations;

namespace Velonixs.Connect.Application.Models;

public sealed record CustomerSummaryResponse(
    Guid Id,
    Guid RestaurantId,
    string PhoneNumber,
    string? Name,
    string? LastAddress,
    int OrderCount,
    DateTimeOffset LastInteractionAt);

public sealed record UpdateCustomerRequest(
    [StringLength(120)]
    string? Name,

    [StringLength(500)]
    string? LastAddress);
