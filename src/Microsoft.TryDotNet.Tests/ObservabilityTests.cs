using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace Microsoft.TryDotNet.Tests;

public class ObservabilityTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void exporters_are_independently_configurable(bool azureMonitor, bool otlp)
    {
        var builder = CreateBuilder(azureMonitor, otlp);

        Assert.Equal(azureMonitor || otlp, Observability.Configure(builder));

        using var services = builder.Services.BuildServiceProvider();
        if (azureMonitor)
        {
            Assert.Equal(
                builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"],
                services.GetRequiredService<IOptions<AzureMonitorOptions>>().Value.ConnectionString);
        }

        if (!azureMonitor && !otlp)
        {
            Assert.Null(services.GetService<TracerProvider>());
            Assert.Null(services.GetService<MeterProvider>());
        }
    }

    [Fact]
    public async Task otlp_configuration_collects_correlated_structured_logs_request_traces_and_metrics()
    {
        var builder = CreateBuilder(azureMonitor: false, otlp: true);
        builder.WebHost.UseTestServer();
        Observability.Configure(builder);

        var traces = new List<Activity>();
        var logs = new List<(string? TraceId, string? Message, object? Value)>();
        var metricNames = new List<string>();
        builder.Services.AddOpenTelemetry()
            .WithTracing(tracing => tracing.AddProcessor(
                new SimpleActivityExportProcessor(new CaptureExporter<Activity>(traces.Add))))
            .WithMetrics(metrics => metrics.AddReader(
                new PeriodicExportingMetricReader(
                    new CaptureExporter<Metric>(metric => metricNames.Add(metric.Name)),
                    exportIntervalMilliseconds: int.MaxValue)));
        builder.Logging.AddOpenTelemetry(logging => logging.AddProcessor(
            new SimpleLogRecordExportProcessor(new CaptureExporter<LogRecord>(record =>
                logs.Add((
                    record.TraceId.ToString(),
                    record.FormattedMessage,
                    record.Attributes?.FirstOrDefault(attribute => attribute.Key == "Operation").Value))))));

        await using var app = builder.Build();
        app.MapGet("/telemetry-probe", (ILogger<ObservabilityTests> logger) =>
        {
            logger.LogInformation("Completed {Operation}", "probe");
            return "OK";
        });
        await app.StartAsync();
        using var client = app.GetTestClient();

        using var response = await client.GetAsync("/telemetry-probe");
        response.EnsureSuccessStatusCode();
        app.Services.GetRequiredService<MeterProvider>().ForceFlush();

        var request = Assert.Single(traces, activity => activity.Kind == ActivityKind.Server);
        Assert.Contains(logs, log =>
            log.TraceId == request.TraceId.ToString() &&
            log.Message == "Completed probe" &&
            Equals(log.Value, "probe"));
        Assert.Contains("http.server.request.duration", metricNames);
        Assert.Contains("dotnet.gc.collections", metricNames);
        var resource = app.Services.GetRequiredService<TracerProvider>().GetResource();
        Assert.Contains(resource.Attributes, attribute =>
            attribute.Key == "service.name" && Equals(attribute.Value, builder.Environment.ApplicationName));
        Assert.Contains(resource.Attributes, attribute =>
            attribute.Key == "deployment.environment.name" && Equals(attribute.Value, "Testing"));
    }

    private static WebApplicationBuilder CreateBuilder(bool azureMonitor, bool otlp)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = "Testing"
        });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = azureMonitor
                ? "InstrumentationKey=00000000-0000-0000-0000-000000000001;IngestionEndpoint=http://127.0.0.1:1/"
                : null,
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = otlp ? "http://127.0.0.1:1" : null
        });
        return builder;
    }

    private sealed class CaptureExporter<T>(Action<T> capture) : BaseExporter<T>
        where T : class
    {
        public override ExportResult Export(in Batch<T> batch)
        {
            foreach (var item in batch)
            {
                capture(item);
            }

            return ExportResult.Success;
        }
    }
}
