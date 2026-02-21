// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace Microsoft.TryDotNet.IntegrationTests;

public class PlaywrightSession : IDisposable
{
    private IPlaywright _playwright;

    public PlaywrightSession(IPlaywright playwright, IBrowser browser)
    {
        _playwright = playwright;
        Browser = browser;
    }

    public IBrowser Browser { get; }

    public static async Task<PlaywrightSession> StartAsync(string browserName = null)
    {
        var exitCode = Playwright.Program.Main(["install"]);
        if (exitCode is not 0)
        {
            throw new Exception($"Playwright exited with code {exitCode}");
        }

        var session = await Playwright.Playwright.CreateAsync().Timeout(TimeSpan.FromMinutes(5), "Timeout creating Playwright session");

        var browserTypeLaunchOptions = new BrowserTypeLaunchOptions();

        if (Debugger.IsAttached)
        {
            browserTypeLaunchOptions.Headless = false;
        }

        var selectedBrowser = (browserName ?? Environment.GetEnvironmentVariable("Playwright.BrowserName") ?? "chromium").ToLowerInvariant();

        IBrowser browser = selectedBrowser switch
        {
            "chromium" => await session.Chromium.LaunchAsync(browserTypeLaunchOptions).Timeout(TimeSpan.FromMinutes(5), "Timeout launching browser"),
            "firefox" => await session.Firefox.LaunchAsync(browserTypeLaunchOptions).Timeout(TimeSpan.FromMinutes(5), "Timeout launching browser"),
            "webkit" => await session.Webkit.LaunchAsync(browserTypeLaunchOptions).Timeout(TimeSpan.FromMinutes(5), "Timeout launching browser"),
            _ => throw new ArgumentException($"Unknown browser '{selectedBrowser}'. Valid values: chromium, firefox, webkit.")
        };

        return new PlaywrightSession(session, browser);
    }

    public void Dispose() => _playwright.Dispose();
}