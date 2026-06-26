using System.ComponentModel.DataAnnotations;

namespace Velonixs.Connect.Contracts.Orders;

public sealed record ConfirmOrderRequest(
    [Range(1, 240)]
    int EstimatedMinutes,
    string? Comment = null);
