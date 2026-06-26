namespace Velonixs.Connect.Mobile.Abstractions;

public sealed record MobileNotificationRequest(
    string Title,
    string Body,
    string? Route = null,
    string? ChannelId = null);

public interface IMobileLocalNotificationService
{
    Task<bool> EnsurePermissionAsync(CancellationToken cancellationToken = default);
    Task ShowAsync(MobileNotificationRequest request, CancellationToken cancellationToken = default);
}

public interface IMobileAlertSoundService
{
    Task PlayNewOrderAsync(CancellationToken cancellationToken = default);
}
