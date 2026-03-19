using System.Net.Http.Json;

namespace Genelife.AdminFrontend.Services;

public sealed class GroceryApiService
{
    private readonly HttpClient _httpClient;

    public GroceryApiService(IConfiguration configuration)
    {
        var apiBase = configuration["ApiBaseUrl"] ?? "http://localhost:5219";
        _httpClient = new HttpClient { BaseAddress = new Uri(apiBase.TrimEnd('/') + "/") };
    }

    public async Task SetPriceAsync(Guid storeId, int? foodPrice, int? drinkPrice, CancellationToken cancellationToken = default)
    {
        await _httpClient.PatchAsJsonAsync(
            $"grocery-store/{storeId}/price",
            new { FoodPrice = foodPrice, DrinkPrice = drinkPrice },
            cancellationToken);
    }
}
