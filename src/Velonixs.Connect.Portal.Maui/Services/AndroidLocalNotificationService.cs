using Android.App;
using Android.Content;
using Android.OS;
using Velonixs.Connect.Mobile.Abstractions;

namespace Velonixs.Connect.Portal.Maui.Services;

public sealed class AndroidLocalNotificationService : IMobileLocalNotificationService
{
    private const string DefaultChannelId = "orders";
    private const string DefaultChannelName = "Order alerts";
    private const string DefaultChannelDescription = "New order and restaurant operations alerts.";

    private readonly NotificationManager notificationManager;

    public AndroidLocalNotificationService()
    {
        notificationManager = Platform.AppContext.GetSystemService(Context.NotificationService) as NotificationManager
            ?? throw new InvalidOperationException("Android notification manager is unavailable.");
    }

    public async Task<bool> EnsurePermissionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureChannel(DefaultChannelId);

        if (!OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            return true;
        }

        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status == PermissionStatus.Granted)
        {
            return true;
        }

        status = await Permissions.RequestAsync<Permissions.PostNotifications>();
        return status == PermissionStatus.Granted;
    }

    public async Task ShowAsync(MobileNotificationRequest request, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!await EnsurePermissionAsync(cancellationToken))
        {
            return;
        }

        var channelId = string.IsNullOrWhiteSpace(request.ChannelId)
            ? DefaultChannelId
            : request.ChannelId;
        EnsureChannel(channelId);

        using var intent = new Intent(Platform.AppContext, typeof(MainActivity));
        intent.SetFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        if (!string.IsNullOrWhiteSpace(request.Route))
        {
            intent.PutExtra("route", request.Route);
        }

        var pendingIntent = PendingIntent.GetActivity(
            Platform.AppContext,
            Random.Shared.Next(),
            intent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var notificationBuilder = OperatingSystem.IsAndroidVersionAtLeast(26)
            ? new Notification.Builder(Platform.AppContext, channelId)
            : new Notification.Builder(Platform.AppContext);

        var notification = notificationBuilder
            .SetContentTitle(request.Title)
            .SetContentText(request.Body)
            .SetSmallIcon(Resource.Mipmap.appicon)
            .SetContentIntent(pendingIntent)
            .SetAutoCancel(true)
            .Build();

        notificationManager.Notify(Random.Shared.Next(), notification);
    }

    private void EnsureChannel(string channelId)
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(26))
        {
            return;
        }

        if (notificationManager.GetNotificationChannel(channelId) is not null)
        {
            return;
        }

        using var channel = new NotificationChannel(channelId, DefaultChannelName, NotificationImportance.High)
        {
            Description = DefaultChannelDescription
        };
        channel.EnableVibration(true);
        notificationManager.CreateNotificationChannel(channel);
    }
}
