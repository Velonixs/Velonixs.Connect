using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;
using Velonixs.Connect.Application.Abstractions;

namespace Velonixs.Connect.Api.Client;

public enum OrderRealtimeConnectionState
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    Reconnecting = 3
}

public interface IOrderRealtimeClient : IAsyncDisposable
{
    event EventHandler<OrderRealtimeEvent>? OrderChanged;
    event EventHandler<OrderRealtimeConnectionState>? ConnectionStateChanged;

    OrderRealtimeConnectionState State { get; }

    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task SubscribeRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken = default);
    Task UnsubscribeRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken = default);
}

public sealed class OrderRealtimeClient(
    IOptions<VelonixsConnectApiClientOptions> options,
    IApiTokenStore tokenStore) : IOrderRealtimeClient
{
    private readonly SemaphoreSlim connectionLock = new(1, 1);
    private readonly OrderRealtimeEventDeduplicator deduplicator = new();
    private HubConnection? connection;
    private OrderRealtimeConnectionState state;

    public event EventHandler<OrderRealtimeEvent>? OrderChanged;
    public event EventHandler<OrderRealtimeConnectionState>? ConnectionStateChanged;

    public OrderRealtimeConnectionState State
    {
        get => state;
        private set
        {
            if (state == value)
            {
                return;
            }

            state = value;
            ConnectionStateChanged?.Invoke(this, value);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await connectionLock.WaitAsync(cancellationToken);
        try
        {
            connection ??= CreateConnection();

            if (connection.State is HubConnectionState.Connected or HubConnectionState.Connecting)
            {
                return;
            }

            State = OrderRealtimeConnectionState.Connecting;
            await connection.StartAsync(cancellationToken);
            State = OrderRealtimeConnectionState.Connected;
        }
        finally
        {
            connectionLock.Release();
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (connection is null)
        {
            return;
        }

        await connection.StopAsync(cancellationToken);
        State = OrderRealtimeConnectionState.Disconnected;
    }

    public async Task SubscribeRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        await StartAsync(cancellationToken);
        await connection!.InvokeAsync("SubscribeRestaurant", restaurantId, cancellationToken);
    }

    public async Task UnsubscribeRestaurantAsync(Guid restaurantId, CancellationToken cancellationToken = default)
    {
        if (connection is null)
        {
            return;
        }

        await connection.InvokeAsync("UnsubscribeRestaurant", restaurantId, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (connection is not null)
        {
            await connection.DisposeAsync();
        }

        connectionLock.Dispose();
    }

    private HubConnection CreateConnection()
    {
        var baseAddress = options.Value.BaseAddress
            ?? throw new InvalidOperationException("API base address must be configured before starting order realtime updates.");
        var hubUri = new Uri(baseAddress, "hubs/orders");
        var hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUri, hubOptions =>
            {
                hubOptions.AccessTokenProvider = async () =>
                    (await tokenStore.GetAsync())?.AccessToken;
            })
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<OrderRealtimeEvent>("OrderChanged", orderEvent =>
        {
            if (deduplicator.TryAccept(orderEvent))
            {
                OrderChanged?.Invoke(this, orderEvent);
            }
        });
        hubConnection.Reconnecting += _ =>
        {
            State = OrderRealtimeConnectionState.Reconnecting;
            return Task.CompletedTask;
        };
        hubConnection.Reconnected += _ =>
        {
            State = OrderRealtimeConnectionState.Connected;
            return Task.CompletedTask;
        };
        hubConnection.Closed += _ =>
        {
            State = OrderRealtimeConnectionState.Disconnected;
            return Task.CompletedTask;
        };

        return hubConnection;
    }
}
