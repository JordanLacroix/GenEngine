using System.Net;
using System.Text;

using GenEngine.PlayerExperience.Application;
using GenEngine.PlayerExperience.Infrastructure;
using GenEngine.Secrets;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GenEngine.Services.Tests;

/// <summary>
/// Proves the real assistant provider is genuinely wired — not the dead code the previous
/// tranche warned about — and, above all, that every failure degrades to <c>null</c> so
/// contextual help falls back to the offline rules. The provider is never reached in these
/// tests: a fake handler stands in for the OpenAI-compatible endpoint.
/// </summary>
public sealed class AssistantAiClientTests
{
    private static readonly AssistantAiContext Context = new(
        "default",
        "scenario",
        HelpModality.Hint,
        2,
        "La note de service",
        "atrium",
        "Vous entrez dans le hall.",
        ["Lire la note", "Passer votre chemin"],
        "Un indice discret",
        false,
        "Tierce",
        "Warm",
        "Socratic");

    [Fact]
    public async Task NoEnabledProviderResolvesOffline()
    {
        RecordingHandler handler = new(HttpStatusCode.OK, "{}");
        AzureFoundryAssistantAiClient client = Build(
            handler,
            [new AssistantAiProvider("Azure", "AzureAiFoundry", false, "https://x/openai/v1", "gpt-4o", "env:TEST_KEY")],
            secret: "resolved");

        string? answer = await client.GenerateAsync(Context, CancellationToken.None);

        Assert.Null(answer);
        Assert.False(handler.WasCalled);
    }

    [Fact]
    public async Task MissingSecretResolvesOffline()
    {
        RecordingHandler handler = new(HttpStatusCode.OK, "{}");
        AzureFoundryAssistantAiClient client = Build(
            handler,
            [new AssistantAiProvider("Azure", "AzureAiFoundry", true, "https://x/openai/v1", "gpt-4o", "env:TEST_KEY")],
            secret: null);

        string? answer = await client.GenerateAsync(Context, CancellationToken.None);

        Assert.Null(answer);
        Assert.False(handler.WasCalled);
    }

    [Fact]
    public async Task ProviderErrorResolvesOffline()
    {
        RecordingHandler handler = new(HttpStatusCode.InternalServerError, "boom");
        AzureFoundryAssistantAiClient client = Build(
            handler,
            [new AssistantAiProvider("Azure", "AzureAiFoundry", true, "https://x/openai/v1", "gpt-4o", "env:TEST_KEY")],
            secret: "resolved");

        string? answer = await client.GenerateAsync(Context, CancellationToken.None);

        Assert.Null(answer);
        Assert.True(handler.WasCalled);
    }

    [Fact]
    public async Task SuccessfulCallReturnsTheGeneratedTextAndAuthenticatesWithApiKeyAndBearer()
    {
        const string body = """
            {"choices":[{"message":{"role":"assistant","content":"  Cherchez ce qui manque.  "}}]}
            """;
        RecordingHandler handler = new(HttpStatusCode.OK, body);
        AzureFoundryAssistantAiClient client = Build(
            handler,
            [new AssistantAiProvider("Azure", "AzureAiFoundry", true, "https://newfoundrymistral-resource.openai.azure.com/openai/v1", "gpt-4o", "env:TEST_KEY")],
            secret: "the-real-key");

        string? answer = await client.GenerateAsync(Context, CancellationToken.None);

        Assert.Equal("Cherchez ce qui manque.", answer);
        Assert.Equal(
            "https://newfoundrymistral-resource.openai.azure.com/openai/v1/chat/completions",
            handler.RequestUri!.ToString());
        Assert.Equal("the-real-key", handler.ApiKeyHeader);
        Assert.Equal("Bearer the-real-key", handler.AuthorizationHeader);
        Assert.Contains("\"model\":\"gpt-4o\"", handler.RequestBody, StringComparison.Ordinal);
        Assert.DoesNotContain("api-version", handler.RequestUri!.Query, StringComparison.Ordinal);
    }

    private static AzureFoundryAssistantAiClient Build(
        RecordingHandler handler,
        IReadOnlyList<AssistantAiProvider> providers,
        string? secret)
    {
        SecretStore store = new([new EnvironmentSecretResolver(name =>
            string.Equals(name, "TEST_KEY", StringComparison.Ordinal) ? secret : null)]);
        IConfiguration configuration = new ConfigurationBuilder().Build();
        return new AzureFoundryAssistantAiClient(
            new CatalogStub(providers),
            store,
            new HttpClient(handler),
            configuration,
            NullLogger<AzureFoundryAssistantAiClient>.Instance);
    }

    private sealed class CatalogStub(IReadOnlyList<AssistantAiProvider> providers) : IAssistantProviderCatalog
    {
        public Task<IReadOnlyList<AssistantAiProvider>> GetAsync(string frontId, CancellationToken cancellationToken) =>
            Task.FromResult(providers);
    }

    private sealed class RecordingHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public bool WasCalled { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ApiKeyHeader { get; private set; }
        public string? AuthorizationHeader { get; private set; }
        public string RequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            WasCalled = true;
            RequestUri = request.RequestUri;
            ApiKeyHeader = request.Headers.TryGetValues("api-key", out IEnumerable<string>? values)
                ? string.Concat(values)
                : null;
            AuthorizationHeader = request.Headers.Authorization?.ToString();
            RequestBody = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }
}
