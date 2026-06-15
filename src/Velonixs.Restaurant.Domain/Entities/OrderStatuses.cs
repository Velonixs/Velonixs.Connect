namespace Velonixs.Restaurant.Domain.Entities;

public static class OrderStatuses
{
    public const string Draft = "Draft";
    public const string PendingConfirmation = "PendingConfirmation";
    public const string Confirmed = "Confirmed";
    public const string Notified = "Notified";
    public const string Handled = "Handled";
    public const string Cancelled = "Cancelled";
    public const string Failed = "Failed";
}
