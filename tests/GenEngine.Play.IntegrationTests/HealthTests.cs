using Microsoft.AspNetCore.Mvc.Testing;

namespace GenEngine.Play.IntegrationTests;

public sealed class HealthTests(WebApplicationFactory<Program> factory) :
    IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task LiveEndpointReturnsSuccess()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health/live", CancellationToken.None);

        response.EnsureSuccessStatusCode();
    }
}
