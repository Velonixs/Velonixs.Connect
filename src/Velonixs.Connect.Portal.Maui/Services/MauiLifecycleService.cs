using Velonixs.Connect.Mobile.Abstractions;

namespace Velonixs.Connect.Portal.Maui.Services;

public sealed class MauiLifecycleService : IMobileLifecycleService
{
    public MobileLifecycleState CurrentState { get; private set; } = MobileLifecycleState.Unknown;

    public event EventHandler<MobileLifecycleState>? StateChanged;

    public void SetState(MobileLifecycleState state)
    {
        if (CurrentState == state)
        {
            return;
        }

        CurrentState = state;
        StateChanged?.Invoke(this, state);
    }
}
