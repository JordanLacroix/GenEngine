using System.Net;

using GenEngine.Play.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

using Polly;
using Polly.CircuitBreaker;

namespace GenEngine.Play.IntegrationTests;

// Verifies the resilience policy wired around the outbound Authoring call:
// bounded retry on transient failures (idempotent GET) and a circuit breaker.
public sealed class AuthoringResilienceTests
{
    [Fact]
    public async Task TransientFailuresAreRetriedThenSucceedWithProductionPolicy()
    {
        var handler = new StubHandler(
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.OK);

        using HttpClient client = BuildClient(
            handler,
            PlayInfrastructureExtensions.ConfigureAuthoringResilience);

        using HttpResponseMessage response = await client.GetAsync(
            new Uri("http://authoring.test/internal/scenario-versions/x"),
            CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, handler.Calls);
    }

    [Fact]
    public async Task CircuitBreakerOpensAfterSustainedFailures()
    {
        var handler = new ThrowingHandler();

        using HttpClient client = BuildClient(handler, options =>
        {
            // Minimal, zero-delay retry so the breaker can be observed opening
            // deterministically without waiting on backoff delays.
            options.Retry.MaxRetryAttempts = 1;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.UseJitter = false;
            options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(1);
            options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(5);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(2);
            options.CircuitBreaker.MinimumThroughput = 2;
            options.CircuitBreaker.FailureRatio = 0.1;
            options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(1);
        });

        var uri = new Uri("http://authoring.test/internal/scenario-versions/x");
        bool circuitOpened = false;

        for (int i = 0; i < 20 && !circuitOpened; i++)
        {
            try
            {
                using HttpResponseMessage response = await client.GetAsync(
                    uri, CancellationToken.None);
            }
            catch (BrokenCircuitException)
            {
                circuitOpened = true;
            }
            catch (HttpRequestException)
            {
                // Expected while the circuit is still closed; keep driving load.
            }
        }

        Assert.True(circuitOpened, "The circuit breaker never opened under sustained failures.");
    }

    private static HttpClient BuildClient(
        HttpMessageHandler primaryHandler,
        Action<HttpStandardResilienceOptions> configure)
    {
        var services = new ServiceCollection();
        services.AddHttpClient("authoring")
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler)
            .AddStandardResilienceHandler(configure);

        ServiceProvider provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IHttpClientFactory>().CreateClient("authoring");
    }

    private sealed class StubHandler(params HttpStatusCode[] statusSequence) : HttpMessageHandler
    {
        private int calls;

        public int Calls => Volatile.Read(ref calls);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            int index = Interlocked.Increment(ref calls) - 1;
            HttpStatusCode status = index < statusSequence.Length
                ? statusSequence[index]
                : statusSequence[^1];
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("Simulated transport failure.");
    }
}