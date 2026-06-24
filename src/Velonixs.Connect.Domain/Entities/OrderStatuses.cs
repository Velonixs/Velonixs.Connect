namespace Velonixs.Connect.Domain.Entities;

public static class OrderStatuses
{
    public const string Draft = "Draft";
    public const string PendingConfirmation = "PendingConfirmation";
    public const string Confirmed = "Confirmed";
    public const string Preparing = "Preparing";
    public const string ReadyForPickup = "ReadyForPickup";
    public const string OutForDelivery = "OutForDelivery";
    public const string Delivered = "Delivered";
    public const string Rejected = "Rejected";
    public const string Notified = "Notified";
    public const string Handled = "Handled";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";

    public static readonly IReadOnlyCollection<string> CustomerVisibleStatuses =
    [
        PendingConfirmation,
        Confirmed,
        Preparing,
        ReadyForPickup,
        OutForDelivery,
        Delivered,
        Rejected,
        Cancelled
    ];
}
