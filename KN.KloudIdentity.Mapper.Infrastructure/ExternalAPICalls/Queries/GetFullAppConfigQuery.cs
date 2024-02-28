using System.Net.Http.Headers;
using System.Text.Json;
using KN.KloudIdentity.Mapper.Domain.Application;
using KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Abstractions;

namespace KN.KloudIdentity.Mapper.Infrastructure.ExternalAPIs.Queries;

public class GetFullAppConfigQuery : IGetFullAppConfigQuery
{
    private readonly HttpClient _httpClient;

    public GetFullAppConfigQuery(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<AppConfig?> GetAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            throw new ArgumentNullException(nameof(appId));
        }

        // Set the token here.
        // _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "token");

        // Call the API here.
        var response = await _httpClient.GetAsync($"/api/applications/{appId}?requireFullInfo=true", cancellationToken);
        response.EnsureSuccessStatusCode();

        // Deserialize the response here.
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        return JsonSerializer.Deserialize<AppConfig>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
    }
}
