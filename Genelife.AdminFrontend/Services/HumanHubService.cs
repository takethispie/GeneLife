using Genelife.AdminFrontend.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Genelife.AdminFrontend.Services;

public sealed class HumanHubService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;

    /// <summary>Raised whenever a HumanUpdate message arrives from the server.</summary>
    public event Action<HumanUpdateDto>? OnHumanUpdate;

    public HubConnectionState State =>
        _hubConnection?.State ?? HubConnectionState.Disconnected;

    public HumanHubService(IConfiguration configuration)
    {
        // Reads "ApiBaseUrl" from appsettings / environment; falls back to localhost for dev.
        var apiBase = configuration["ApiBaseUrl"] ?? "http://localhost:5219";
        _hubUrl = $"{apiBase.TrimEnd('/')}/hubs/human";
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection is not null)
            return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<HumanUpdateDto>("HumanUpdate", dto =>
        {
            OnHumanUpdate?.Invoke(dto);
        });

        await _hubConnection.StartAsync(cancellationToken);

        // Subscribe to the "all-humans" group so we receive every person's updates.
        await _hubConnection.InvokeAsync("SubscribeToHuman", "all-humans", cancellationToken);
    }

    public async Task SubscribeToHumanAsync(Guid humanId, CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("Hub connection has not been started.");

        await _hubConnection.InvokeAsync("SubscribeToHuman", humanId.ToString(), cancellationToken);
    }

    public async Task UnsubscribeFromHumanAsync(Guid humanId, CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null)
            return;

        await _hubConnection.InvokeAsync("UnsubscribeFromHuman", humanId.ToString(), cancellationToken);
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
