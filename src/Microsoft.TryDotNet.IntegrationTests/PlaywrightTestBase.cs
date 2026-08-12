// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Pocket;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true)]

namespace Microsoft.TryDotNet.IntegrationTests;

[Collection(nameof(IntegratedServicesFixture))]
public abstract class PlaywrightTestBase : IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    protected PlaywrightTestBase(
        IntegratedServicesFixture services,
        ITestOutputHelper output)
    {
        Services = services;
        Output = output;
    }

    public IntegratedServicesFixture Services { get; }

    public ITestOutputHelper Output { get; }

    protected async Task<IPage> NewPageAsync()
    {
        var playwright = await Services.GetPlaywrightAsync();
        var page = await playwright.Browser.NewPageAsync();
        // Firefox/WASM apps keep persistent connections (SignalR, hot-reload) that prevent
        // NetworkIdle from ever firing. Use generous timeouts for both navigation and locator waits.
        page.SetDefaultNavigationTimeout(90_000f);
        page.SetDefaultTimeout(90_000f);
        return page;
    }

    protected async Task<Uri> TryDotNetUrlAsync()
    {
        var server = await Services.GetTryDotNetServerAsync();
        return server.Url;
    }

    protected async Task<Uri> LearnUrlAsync()
    {
        var server = await Services.GetLearnServerAsync();
        return server.Url;
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}

