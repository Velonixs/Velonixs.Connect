using Velonixs.Connect.Mobile.Abstractions;
using Velonixs.Connect.Portal.Maui.Services;

namespace Velonixs.Connect.Portal.Maui;

public partial class App : Microsoft.Maui.Controls.Application
{
    private readonly MauiLifecycleService lifecycleService;

    public App(MauiLifecycleService lifecycleService)
    {
        this.lifecycleService = lifecycleService;
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        lifecycleService.SetState(MobileLifecycleState.Resumed);
        return new Window(new MainPage()) { Title = "Velonixs Restaurant Portal" };
    }

    protected override void OnSleep()
    {
        lifecycleService.SetState(MobileLifecycleState.Paused);
        base.OnSleep();
    }

    protected override void OnResume()
    {
        lifecycleService.SetState(MobileLifecycleState.Resumed);
        base.OnResume();
    }
}
