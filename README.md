# Try .NET <img src ="https://user-images.githubusercontent.com/2546640/56708992-deee8780-66ec-11e9-9991-eb85abb1d10a.png" width="80px" alt="dotnet bot in space" align ="right">

## Welcome to the Try .NET repo.

Try .NET provides developers and content authors with tools to create interactive experiences.

There are two different Try .NET interactive experiences:
* The web experience powered by Blazor (as seen on [Essential C#](https://essentialcsharp.com/hello-world#hello-world))
* Interactive documentation with Markdown for .NET Core with the [dotnet try global tool](DotNetTry.md).

### Online, powered by Blazor

EssentialCSharp.com uses Try .NET to create interactive code listings. Users can run and edit code all in the browser.

_**Please Note**: At the moment, the Try .NET online (`trydotnet.js API`) is currently only used internally at IntelliTect as seen on EssentialCSharp.com(https://essentialcsharp.com/hello-world#hello-world)._

<img src ="https://user-images.githubusercontent.com/2546640/57144765-c850cc00-6d8f-11e9-982d-50d2b6dc3591.gif" width = "80%">

### Interactive .NET Core documentation with the `dotnet try` global tool

Try .NET enables .NET developers to create interactive markdown files.
To make your markdown files interactive, you will need the [.NET Core 3.0 SDK](https://dotnet.microsoft.com/download/dotnet-core/3.0), the `dotnet try` global tool and [Visual Studio](https://visualstudio.microsoft.com/) / [VS Code](https://code.visualstudio.com/) (or any other editor of your choice).

<img src ="https://user-images.githubusercontent.com/2546640/57158389-47a2c780-6db1-11e9-96ad-8c6e9ab52853.png" width = "80%">

## Table of Contents
- [Getting Started with Try .NET](DotNetTryLocal.md): Interactive documentation generator for .NET Core.

## Observability

The API uses OpenTelemetry for correlated structured `ILogger` logs, incoming HTTP
request and outgoing `HttpClient` traces, HTTP metrics, and built-in .NET runtime
metrics. Console logs are JSON, so Azure Container Apps can collect them separately
from application telemetry. The optional `POCKETLOGGER_LOG_PATH` diagnostic files
are separate and are not exported.

Telemetry export is enabled by configuration:

| Setting | Destination |
| --- | --- |
| `APPLICATIONINSIGHTS_CONNECTION_STRING` | Azure Monitor Application Insights (logs, traces, metrics, and the existing profiler) |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Aspire dashboard or another OTLP collector (logs, traces, metrics) |

Both destinations can be enabled simultaneously. Neither is required for local
execution; without either setting the API logs a warning and does not export.
`OTEL_SERVICE_NAME` and `OTEL_RESOURCE_ATTRIBUTES` override the default service
identity. The hosting environment is recorded as `deployment.environment.name`.
Informational application logs are exported by default; ASP.NET Core framework
logs remain at Warning to limit noise. Adjust `Logging:OpenTelemetry:LogLevel`
to change exported log levels.

### Azure Container Apps

Use a workspace-based Application Insights resource for durable production
telemetry. Set its connection string on the **running Container App**, not just
the GitHub Actions runner. For example, with the Azure CLI authenticated and
`APPLICATIONINSIGHTS_CONNECTION_STRING` loaded securely into your shell:

```powershell
az containerapp secret set --name <app> --resource-group <group> `
  --secrets "applicationinsights-connection-string=$env:APPLICATIONINSIGHTS_CONNECTION_STRING" `
  --output none
az containerapp update --name <app> --resource-group <group> `
  --set-env-vars "APPLICATIONINSIGHTS_CONNECTION_STRING=secretref:applicationinsights-connection-string" `
    "OTEL_SERVICE_NAME=trydotnet" `
  --output none
```

Configure Development and Production apps separately with their respective
Application Insights resources. Prefer a Key Vault-backed Container Apps secret
when managed by your infrastructure tooling; never commit connection strings.
The deployment workflow updates only the image and preserves these app settings.
Resource provisioning, retention, sampling, alerts, and access control belong in
the Azure infrastructure configuration and are not created by this application.

After deployment, call `/sensors/version` and check Application Insights
**Transaction search**, **Application map**, **Logs**, and **Metrics**. In the linked
Log Analytics workspace, query `AppRequests`, `AppDependencies`, `AppTraces`,
`AppExceptions`, and `AppMetrics`; use `OperationId` to correlate requests and logs.
Allow for ingestion delay. Container console logs alone do not provide distributed
traces or application metrics.

### Aspire dashboard or another OTLP collector

No Aspire AppHost is required. Point `OTEL_EXPORTER_OTLP_ENDPOINT` at a reachable
collector, set `OTEL_EXPORTER_OTLP_PROTOCOL` to `grpc` (typically port 4317) or
`http/protobuf` (typically port 4318), and use `OTEL_EXPORTER_OTLP_HEADERS` for
authentication when required. For the standalone Aspire dashboard, use its OTLP
endpoint and the configured `x-otlp-api-key` header, not its browser URL.
Keep the dashboard and ingestion endpoint private and authenticated.

The [Aspire telemetry-after-deployment guide](https://aspire.dev/dashboard/telemetry-after-deployment/)
describes this same pattern: use the dashboard for development or short-term
diagnostics and Azure Monitor (or another durable backend) for production.

## Contribution Guidelines

If you find an issue or have a feature suggestion, please open an [issue](https://github.com/intellitect/try/issues/new/choose). And if you have any feature suggestions, please submit them using the "community suggestions" label.

## Customers & Partners

**Essential C#**

Essential C# uses [Try .NET](https://essentialcsharp.com/hello-world#hello-world) to create interactive code samples. Users can run and edit .NET code in the browser.
