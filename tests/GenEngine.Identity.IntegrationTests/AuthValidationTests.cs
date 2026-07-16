using System.Net;
using System.Net.Http.Json;

using Microsoft.AspNetCore.Mvc.Testing;

namespace GenEngine.Identity.IntegrationTests;

// Regression: malformed credentials (missing/blank fields) previously reached
// the service with null values and threw a NullReferenceException, surfacing as
// HTTP 500. They must be rejected as 400 before any repository/database access,
// so these cases run without a live database.
public sealed class AuthValidationTests(WebApplicationFactory<Program> factory) :
    IClassFixture<WebApplicationFactory<Program>>
{
    public static TheoryData<string, object> MalformedRequests() => new()
    {
        { "/auth/login", new { } },
        { "/auth/login", new { userName = "", password = "whatever12345" } },
        { "/auth/login", new { userName = "someone", password = "" } },
        { "/auth/register", new { } },
        { "/auth/register", new { userName = "", password = "whatever12345" } },
    };

    [Theory]
    [MemberData(nameof(MalformedRequests))]
    public async Task MalformedCredentialsReturnBadRequestNotServerError(string path, object body)
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(path, body, CancellationToken.None);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}