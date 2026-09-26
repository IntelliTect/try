// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.IO;
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

    private const string BrowserEnvironmentVariableName = "BROWSER";

    public static async Task<PlaywrightSession> StartAsync()
    {
        var browserName = (Environment.GetEnvironmentVariable(BrowserEnvironmentVariableName) is { Length: > 0 } value
                               ? value
                               : "chromium").ToLowerInvariant();

        Func<IPlaywright, IBrowserType> selectBrowserType = browserName switch
        {
            "chromium" => p => p.Chromium,
            "firefox" => p => p.Firefox,
            "webkit" => p => p.Webkit,
            _ => throw new InvalidOperationException($"Unsupported value '{browserName}' for environment variable '{BrowserEnvironmentVariableName}'. Expected 'chromium', 'firefox', or 'webkit'.")
        };

        var exitCode = Playwright.Program.Main(["install", browserName]);
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

        var browser = await selectBrowserType(session).LaunchAsync(browserTypeLaunchOptions).Timeout(TimeSpan.FromMinutes(5), "Timeout launching browser");

        await LogBrowserInfoAsync(browserName, browser);

        return new PlaywrightSession(session, browser);
    }

    // Browser behavior (e.g. which key bindings Monaco picks) depends on what the browser reports
    // about itself, which doesn't always match the host OS, so record it in the test output.
    private static async Task LogBrowserInfoAsync(string browserName, IBrowser browser)
    {
        var page = await browser.NewPageAsync();
        try
        {
            var userAgent = await page.EvaluateAsync<string>("() => navigator.userAgent");
            var platform = await page.EvaluateAsync<string>("() => navigator.platform");
            var info = $"browser: {browserName} {browser.Version}; navigator.platform: {platform}; navigator.userAgent: {userAgent}";
            Console.WriteLine($"[PlaywrightSession] {info}");

            // dotnet test doesn't surface console output from the test host, so in GitHub Actions
            // also write it to the job summary.
            if (Environment.GetEnvironmentVariable("GITHUB_STEP_SUMMARY") is { Length: > 0 } summaryPath)
            {
                await File.AppendAllTextAsync(summaryPath, $"`{info}`{Environment.NewLine}");
            }
        }
        finally
        {
            await page.CloseAsync();
        }
    }

    public void Dispose() => _playwright.Dispose();
}