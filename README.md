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

The API exports OpenTelemetry logs, traces, and metrics when an exporter is configured:

- **Azure Monitor:** Set `APPLICATIONINSIGHTS_CONNECTION_STRING` on the Azure Container
  App, preferably using a Key Vault-backed secret.
- **Aspire dashboard or another OTLP collector:** Set `OTEL_EXPORTER_OTLP_ENDPOINT`.
  Configure `OTEL_EXPORTER_OTLP_PROTOCOL` and `OTEL_EXPORTER_OTLP_HEADERS` if needed.

## Listing compilation tests

`Microsoft.TryDotNet.FileIntegration.Tests` compiles whole C# files through the API.
Set `TRYDOTNET_LISTINGS_PATH` to prepared listing sources and
`TRYDOTNET_LISTINGS_METADATA` to their `chapter-listings.json` to include external
listings. Keep `TRYDOTNET_UPDATE_LISTINGS=false` when validating expectations.

The harness replaces only Roslyn-parsed `#region` and `#endregion` directives with
spaces, preserving line breaks and diagnostic positions. Presentation regions,
including duplicate names or orphan closers, do not affect whole-file compilation;
strings, comments, and conditional compilation directives remain unchanged. This
normalization does not modify packaged listings or interactive region extraction.

Successful compilation requires an emitted, nonempty assembly. Type-only listings
without an entry point compile as libraries; other compilation errors still fail.
The console project's SDK-generated implicit usings are preserved on both Windows
and Linux. Template-generated assembly metadata is omitted so submitted assembly
attributes do not conflict with template defaults; duplicate attributes within
submitted source remain errors.

## Contribution Guidelines

If you find an issue or have a feature suggestion, please open an [issue](https://github.com/intellitect/try/issues/new/choose). And if you have any feature suggestions, please submit them using the "community suggestions" label.

## Customers & Partners

**Essential C#**

Essential C# uses [Try .NET](https://essentialcsharp.com/hello-world#hello-world) to create interactive code samples. Users can run and edit .NET code in the browser.
