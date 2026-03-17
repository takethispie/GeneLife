using Genelife.AdminFrontend.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Genelife.AdminFrontend.Services;

public sealed class HumanHubService : IAsyncDisposable
{
    private HubConnection? hubConnection;
    private readonly string hubUrl;

    public event Action<HumanUpdateDto>? OnHumanUpdate;

    public HubConnectionState State =>
        hubConnection?.State ?? HubConnectionState.Disconnected;

    public HumanHubService(IConfiguration configuration)
    {
        var apiBase = configuration["ApiBaseUrl"] ?? "http://localhost:5219";
        hubUrl = $"{apiBase.TrimEnd('/')}/hubs/human";
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (hubConnection is not null)
            return;

        hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<HumanUpdateDto>("HumanUpdate", dto =>
        {
            OnHumanUpdate?.Invoke(dto);
        });

        await hubConnection.StartAsync(cancellationToken);
        await hubConnection.InvokeAsync("SubscribeToHuman", "all-humans", cancellationToken);
    }

    public async Task SubscribeToHumanAsync(Guid humanId, CancellationToken cancellationToken = default)
    {
        if (hubConnection is null)
            throw new InvalidOperationException("Hub connection has not been started.");
        await hubConnection.InvokeAsync("SubscribeToHuman", humanId.ToString(), cancellationToken);
    }

    public async Task UnsubscribeFromHumanAsync(Guid humanId, CancellationToken cancellationToken = default)
    {
        if (hubConnection is null)
            return;
        await hubConnection.InvokeAsync("UnsubscribeFromHuman", humanId.ToString(), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
            hubConnection = null;
        }
    }
}
