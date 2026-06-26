using Velonixs.Connect.Mobile.Abstractions;

namespace Velonixs.Connect.Portal.Maui.Services;

public sealed class MauiConnectivityService : IMobileConnectivityService, IDisposable
{
    public MauiConnectivityService()
    {
        Connectivity.Current.ConnectivityChanged += OnConnectivityChanged;
    }

    public MobileConnectivityStatus CurrentStatus => Map(Connectivity.Current.NetworkAccess);

    public event EventHandler<MobileConnectivityStatus>? StatusChanged;

    public void Dispose()
    {
        Connectivity.Current.ConnectivityChanged -= OnConnectivityChanged;
    }

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs args)
    {
        StatusChanged?.Invoke(this, Map(args.NetworkAccess));
    }

    private static MobileConnectivityStatus Map(NetworkAccess networkAccess) =>
        networkAccess switch
        {
            NetworkAccess.Internet => MobileConnectivityStatus.Online,
            NetworkAccess.ConstrainedInternet => MobileConnectivityStatus.Constrained,
            NetworkAccess.None => MobileConnectivityStatus.Offline,
            _ => MobileConnectivityStatus.Unknown
        };
}
