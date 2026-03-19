using Genelife.AdminFrontend.Models;
using Microsoft.AspNetCore.SignalR.Client;

namespace Genelife.AdminFrontend.Services;

public sealed class CompanyHubService : IAsyncDisposable
{
    private HubConnection? hubConnection;
    private readonly string hubUrl;

    public event Action<CompanyUpdateDto>? OnCompanyUpdate;

    public HubConnectionState State =>
        hubConnection?.State ?? HubConnectionState.Disconnected;

    public CompanyHubService(IConfiguration configuration)
    {
        var apiBase = configuration["ApiBaseUrl"] ?? "http://localhost:5219";
        hubUrl = $"{apiBase.TrimEnd('/')}/hubs/company";
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (hubConnection is not null)
            return;

        hubConnection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();

        hubConnection.On<CompanyUpdateDto>("CompanyUpdate", dto =>
        {
            OnCompanyUpdate?.Invoke(dto);
        });

        await hubConnection.StartAsync(cancellationToken);
        await hubConnection.InvokeAsync("SubscribeToCompany", "all-companies", cancellationToken);
    }

    public async Task SubscribeToCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (hubConnection is null)
            throw new InvalidOperationException("Hub connection has not been started.");
        await hubConnection.InvokeAsync("SubscribeToCompany", companyId.ToString(), cancellationToken);
    }

    public async Task UnsubscribeFromCompanyAsync(Guid companyId, CancellationToken cancellationToken = default)
    {
        if (hubConnection is null)
            return;
        await hubConnection.InvokeAsync("UnsubscribeFromCompany", companyId.ToString(), cancellationToken);
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
