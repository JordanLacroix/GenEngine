using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace GenEngine.Observability;

public static class ObservabilityExtensions
{
    public static IServiceCollection AddGenEngineObservability(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        services.AddSingleton<IAuditLog, AuditLog>();

        bool exportOtlp = !string.IsNullOrWhiteSpace(configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        OpenTelemetryBuilder telemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName));

        telemetry.WithTracing(tracing =>
        {
            tracing
                .AddAspNetCoreInstrumentation(options =>
                    options.Filter = context =>
                        !context.Request.Path.StartsWithSegments("/health"))
                .AddHttpClientInstrumentation();

            if (exportOtlp)
            {
                tracing.AddOtlpExporter();
            }
        });

        telemetry.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation();

            if (exportOtlp)
            {
                metrics.AddOtlpExporter();
            }
        });

        services.Configure<LoggerFactoryOptions>(options =>
        {
            options.ActivityTrackingOptions =
                ActivityTrackingOptions.TraceId |
                ActivityTrackingOptions.SpanId |
                ActivityTrackingOptions.ParentId;
        });

        services.AddLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddJsonConsole(options =>
            {
                options.IncludeScopes = true;
                options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
                options.UseUtcTimestamp = true;
            });

            if (exportOtlp)
            {
                logging.AddOpenTelemetry(options =>
                {
                    options.IncludeFormattedMessage = true;
                    options.IncludeScopes = true;
                    options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(serviceName));
                    options.AddOtlpExporter();
                });
            }
        });

        return services;
    }
}