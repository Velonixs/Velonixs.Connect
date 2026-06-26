using Velonixs.Connect.Domain.Entities;

namespace Velonixs.Connect.Domain.Services;

public static class OrderStatusTransitionPolicy
{
    private static readonly IReadOnlyDictionary<string, string[]> AllowedTransitions =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [OrderStatuses.PendingConfirmation] =
            [
                OrderStatuses.Confirmed,
                OrderStatuses.Rejected,
                OrderStatuses.Cancelled
            ],
            [OrderStatuses.Confirmed] =
            [
                OrderStatuses.Preparing,
                OrderStatuses.ReadyForPickup,
                OrderStatuses.OutForDelivery,
                OrderStatuses.Delivered,
                OrderStatuses.Cancelled
            ],
            [OrderStatuses.Preparing] =
            [
                OrderStatuses.ReadyForPickup,
                OrderStatuses.OutForDelivery,
                OrderStatuses.Delivered,
                OrderStatuses.Cancelled
            ],
            [OrderStatuses.ReadyForPickup] =
            [
                OrderStatuses.Delivered,
                OrderStatuses.Cancelled
            ],
            [OrderStatuses.OutForDelivery] =
            [
                OrderStatuses.Delivered,
                OrderStatuses.Cancelled
            ],
            [OrderStatuses.Notified] =
            [
                OrderStatuses.Preparing,
                OrderStatuses.ReadyForPickup,
                OrderStatuses.OutForDelivery,
                OrderStatuses.Delivered,
                OrderStatuses.Cancelled
            ],
            [OrderStatuses.Handled] =
            [
                OrderStatuses.Delivered,
                OrderStatuses.Cancelled
            ]
        };

    public static string Normalize(string status)
    {
        var trimmed = status.Trim();
        var knownStatus = OrderStatuses.CustomerVisibleStatuses
            .Concat([OrderStatuses.Notified, OrderStatuses.Handled, OrderStatuses.Failed])
            .FirstOrDefault(x => string.Equals(x, trimmed, StringComparison.OrdinalIgnoreCase));

        return knownStatus ?? throw new InvalidOperationException($"Unsupported order status '{status}'.");
    }

    public static bool CanTransition(string currentStatus, string newStatus)
    {
        var normalizedCurrentStatus = Normalize(currentStatus);
        var normalizedNewStatus = Normalize(newStatus);

        return AllowedTransitions.TryGetValue(normalizedCurrentStatus, out var allowed) &&
               allowed.Contains(normalizedNewStatus, StringComparer.OrdinalIgnoreCase);
    }

    public static void ValidateTransition(
        string currentStatus,
        string newStatus,
        string? comment,
        int? estimatedMinutes)
    {
        var normalizedCurrentStatus = Normalize(currentStatus);
        var normalizedNewStatus = Normalize(newStatus);

        if (normalizedNewStatus == OrderStatuses.Confirmed &&
            (estimatedMinutes is null or <= 0 or > 1440))
        {
            throw new InvalidOperationException("Estimated time is required when confirming an order.");
        }

        if (normalizedNewStatus == OrderStatuses.Rejected && string.IsNullOrWhiteSpace(comment))
        {
            throw new InvalidOperationException("Rejection reason is required.");
        }

        if (!AllowedTransitions.TryGetValue(normalizedCurrentStatus, out var allowed) ||
            !allowed.Contains(normalizedNewStatus, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Order cannot move from {normalizedCurrentStatus} to {normalizedNewStatus}.");
        }
    }
}
