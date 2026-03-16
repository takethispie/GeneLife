using Genelife.AdminFrontend.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Genelife.AdminFrontend.Services;

public sealed class CompanyHubService : IAsyncDisposable
{
    private HubConnection? _hubConnection;
    private readonly string _hubUrl;

    /// <summary>Raised whenever a CompanyUpdate message arrives from the server.</summary>
    public event Action<CompanyUpdateDto>? OnCompanyUpdate;

    public HubConnectionState State =>
        _hubConnection?.State ?? HubConnectionState.Disconnected;

    public CompanyHubService(IConfiguration configuration)
    {
        var apiBase = configuration["ApiBaseUrl"] ?? "http://localhost:5219";
        _hubUrl = $"{apiBase.TrimEnd('/')}/hubs/company";
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_hubConnection is not null)
            return;

        _hubConnection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<CompanyUpdateDto>("CompanyUpdate", dto =>
        {
            OnCompanyUpdate?.Invoke(dto);
        });

        await _hubConnection.StartAsync(cancellationToken);

        // Subscribe to the "all-companies" group to receive every company's updates.
        await _hubConnection.InvokeAsync("SubscribeToCompany", "all-companies", cancellationToken);
    }

    public async Task SubscribeToCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null)
            throw new InvalidOperationException("Hub connection has not been started.");

        await _hubConnection.InvokeAsync("SubscribeToCompany", companyId.ToString(), cancellationToken);
    }

    public async Task UnsubscribeFromCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (_hubConnection is null)
            return;

        await _hubConnection.InvokeAsync("UnsubscribeFromCompany", companyId.ToString(), cancellationToken);
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
