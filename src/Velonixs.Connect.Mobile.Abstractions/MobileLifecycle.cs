namespace Velonixs.Connect.Mobile.Abstractions;

public enum MobileLifecycleState
{
    Unknown = 0,
    Resumed = 1,
    Paused = 2,
    Stopped = 3
}

public interface IMobileLifecycleService
{
    MobileLifecycleState CurrentState { get; }
    event EventHandler<MobileLifecycleState>? StateChanged;
}
