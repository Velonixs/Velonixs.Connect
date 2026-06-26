using Velonixs.Connect.Mobile.Abstractions;

namespace Velonixs.Connect.Portal.Maui.Services;

public sealed class MauiAlertSoundService : IMobileAlertSoundService
{
    public Task PlayNewOrderAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(250));
        return Task.CompletedTask;
    }
}
