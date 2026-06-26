namespace Velonixs.Connect.Mobile.Abstractions;

public enum MobileConnectivityStatus
{
    Unknown = 0,
    Offline = 1,
    Constrained = 2,
    Online = 3
}

public interface IMobileConnectivityService
{
    MobileConnectivityStatus CurrentStatus { get; }
    event EventHandler<MobileConnectivityStatus>? StatusChanged;
}
