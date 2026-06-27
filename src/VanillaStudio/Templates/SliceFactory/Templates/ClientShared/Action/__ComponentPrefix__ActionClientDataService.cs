using {{RootNamespace}}.Framework;
using {{RootNamespace}}.ServiceContracts.Features.__moduleNamespace__;

namespace {{RootNamespace}}.ClientShared.Features.__moduleNamespace__;

internal class __ComponentPrefix__ActionClientDataService : I__ComponentPrefix__ActionDataService
{
    private readonly BaseHttpClient _httpClient;

    public __ComponentPrefix__ActionClientDataService(BaseHttpClient httpClient)
        => _httpClient = httpClient;

    public async Task ExecuteAsync(__primaryKeyType__ id)
    {
        await _httpClient.PostAsJsonAsync<object>(
            $"api/__ComponentPrefix__Action/{id}/execute", new { });
    }
}
