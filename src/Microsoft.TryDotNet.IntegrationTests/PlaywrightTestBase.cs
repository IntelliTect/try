// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Pocket;
using Xunit;
using Xunit.Abstractions;

[assembly: CollectionBehavior(CollectionBehavior.CollectionPerAssembly, DisableTestParallelization = true)]

namespace Microsoft.TryDotNet.IntegrationTests;

[Collection(nameof(IntegratedServicesFixture))]
public abstract class PlaywrightTestBase : IDisposable, IAsyncLifetime
{
    private readonly CompositeDisposable _disposables = new();
    private readonly List<IPage> _pages = new();

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
        _pages.Add(page);
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

    public Task InitializeAsync() => Task.CompletedTask;

    // The browser is shared across all tests, so close each test's pages to stop them
    // (and their WASM runtimes) from accumulating over the run.
    public async Task DisposeAsync()
    {
        foreach (var page in _pages)
        {
            await page.CloseAsync();
        }
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }
}

