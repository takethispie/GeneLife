using System.Net.Http.Json;
using Genelife.AdminFrontend.Models;
using Genelife.Domain;

namespace Genelife.AdminFrontend.Services;

public sealed class HumanApiService
{
    private readonly HttpClient _httpClient;

    public HumanApiService(IConfiguration configuration)
    {
        var apiBase = configuration["ApiBaseUrl"] ?? "http://localhost:5219";
        _httpClient = new HttpClient { BaseAddress = new Uri(apiBase.TrimEnd('/') + "/") };
    }

    public async Task<Guid?> CreateHumanAsync(CreateHumanDto humanDto,
        CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync(
            "create/human/custom",
            humanDto,
            cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        var result = await response.Content.ReadFromJsonAsync<CreateHumanResponse>(cancellationToken: cancellationToken);
        return result?.HumanId;
    }

    private record CreateHumanResponse(Guid HumanId);
}
