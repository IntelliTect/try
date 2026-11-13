using Azure.Monitor.OpenTelemetry.AspNetCore;
using Azure.Monitor.OpenTelemetry.Profiler;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Microsoft.TryDotNet;

public static class Observability
{
    public static bool Configure(WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"];
        var useAzureMonitor = !string.IsNullOrWhiteSpace(connectionString);
        var useOtlp = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (!useAzureMonitor && !useOtlp)
        {
            return false;
        }

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        var telemetry = builder.Services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                .AddService(builder.Environment.ApplicationName)
                .AddAttributes(new Dictionary<string, object>
                {
                    ["deployment.environment.name"] = builder.Environment.EnvironmentName
                })
                // Preserve standard OTEL_SERVICE_NAME and OTEL_RESOURCE_ATTRIBUTES overrides.
                .AddEnvironmentVariableDetector())
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddMeter("System.Runtime"));

        if (useAzureMonitor)
        {
            telemetry
                .UseAzureMonitor(options => options.ConnectionString = connectionString)
                .AddAzureMonitorProfiler();
        }
        else
        {
            telemetry.WithTracing(tracing => tracing
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation());
        }

        if (useOtlp)
        {
            telemetry.UseOtlpExporter();
        }

        return true;
    }
}
