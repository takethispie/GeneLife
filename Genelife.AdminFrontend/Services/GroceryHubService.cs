using Genelife.AdminFrontend.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Genelife.AdminFrontend.Services;

public sealed class GroceryHubService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;

    public event Action<GroceryUpdateDto>? OnGroceryUpdate;

    public HubConnectionState State =>
        _hubConnection?.State ?? HubConnectionState.Disconnected;

    public GroceryHubService(IConfiguration configuration)
    {
        var apiBase = configuration["ApiBaseUrl"] ?? "http://localhost:5219";
        _hubUrl = $"{apiBase.TrimEnd('/')}/hubs/grocery";
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection is not null)
            return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<GroceryUpdateDto>("GroceryUpdate", dto =>
        {
            OnGroceryUpdate?.Invoke(dto);
        });

        await _hubConnection.StartAsync(cancellationToken);
        await _hubConnection.InvokeAsync("SubscribeToGroceryStore", "all-grocery-stores", cancellationToken);
    }

    public async Task SubscribeToStoreAsync(Guid storeId, CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("Hub connection has not been started.");

        await _hubConnection.InvokeAsync("SubscribeToGroceryStore", storeId.ToString(), cancellationToken);
    }

    public async Task UnsubscribeFromStoreAsync(Guid storeId, CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null)
            return;

        await _hubConnection.InvokeAsync("UnsubscribeFromGroceryStore", storeId.ToString(), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
            _hubConnection = null;
        }
    }
}
