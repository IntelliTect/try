// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Playwright;

namespace Microsoft.TryDotNet.IntegrationTests;

internal static class PageExtensions
{
    public static Task<byte[]> TestScreenShotAsync(this IPage page, string? label = null, [CallerMemberName] string? testName = null, [CallerFilePath] string? sourceFilePath = null)
    {
        var imageName = $"{Path.GetFileNameWithoutExtension(sourceFilePath)}_{testName}";
        if (!string.IsNullOrWhiteSpace(label))
        {
            imageName = $"{imageName}_{label}";
        }
        return page.ScreenshotAsync(new PageScreenshotOptions
        {
            Path = Path.Combine("playwright_screenshots", $"screenshot_{imageName}.png"),
            FullPage = true
        });
    }

    public static async Task DispatchMessage<T>(this IPage page, T data)
    {
        await page.EvaluateAsync(@"(request) => {
window.dispatchEvent(new MessageEvent(""message"", { data: request }));
}", data);
    }

    public static async Task<ILocator> FindEditor(this IPage page)
    {
        var editor = page.Locator(@"[role = ""textbox""]");
        await editor.IsVisibleAsync();
        return editor;
    }

    public static async Task<ILocator> FindEditorContent(this IPage page)
    {
        var editor = page.Locator(@"div[role = ""presentation""] .view-lines");
        await editor.IsVisibleAsync();
        return editor;
    }

    public static async Task<ILocator> FindEditorContent(this ILocator locator)
    {
        var editor = locator.Locator(@"div[role = ""presentation""] .view-lines");
        await editor.IsVisibleAsync();
        return editor;
    }
    public static async Task<ILocator> FindEditorContent(this IFrameLocator iframe)
    {
        var editor = iframe.Locator(@"div[role = ""presentation""] .view-lines");
        await editor.IsVisibleAsync();
        return editor;
    }

    public static async Task TypeTextInMonacoEditor(this IPage page, string text, float? delay = 30)
    {
        var editor = await page.FindEditor();
        await editor.FocusAsync();
        await editor.PressSequentiallyAsync(text, new LocatorPressSequentiallyOptions { Delay = delay });
    }

    public static async Task<string> GetEditorContentAsync(this IPage page)
    {
        var editor = await page.FindEditorContent();
        var text = await editor.TextContentAsync()?? string.Empty;
        return text;
    }

    public static async Task<string> GetEditorContentAsync(this ILocator locator)
    {
        var editor = await locator.FindEditorContent();
        var text = await editor.TextContentAsync() ?? string.Empty;
        return text;
    }

    public static async Task<string> GetEditorContentAsync(this IFrameLocator iframe)
    {
        var editor = await iframe.FindEditorContent();
        var text = await editor.TextContentAsync() ?? string.Empty;
        return text;
    }

    private static float ReadyTimeout => Debugger.IsAttached ? 0.0f : (float)TimeSpan.FromMinutes(2).TotalMilliseconds;

    // Waits for the editor bootstrapper to finish; it sets window.trydotnetEditor right
    // before posting HostEditorReady. This avoids LoadState.NetworkIdle, which never settles
    // reliably in some browsers (notably Firefox) on the editor page.
    public static async Task WaitForEditorReadyAsync(this IPage page)
    {
        await page.WaitForFunctionAsync("() => window.trydotnetEditor !== undefined", null, new PageWaitForFunctionOptions { Timeout = ReadyTimeout });
    }

    // The WASM runner appends this sentinel once it is ready to accept wasmRunner-command messages.
    public static async Task WaitForWasmRunnerReadyAsync(this IPage page)
    {
        await page.Locator("#wasmRunner-sentinel").WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Attached, Timeout = ReadyTimeout });
    }

    public static async Task ClearMonacoEditor(this IPage page)
    {
        var editor = page.Locator(@"[role = ""textbox""]");
        await editor.IsVisibleAsync();
        await editor.FocusAsync();
        // Select all through the Monaco API rather than a keyboard shortcut: Monaco picks its
        // key bindings from the browser's reported platform, so the select-all shortcut isn't
        // consistent across browser/OS combinations (e.g. Ctrl+A didn't select all in WebKit on Linux).
        await page.EvaluateAsync(@"() => {
const monacoEditor = window.trydotnetEditor.editor._editor;
monacoEditor.setSelection(monacoEditor.getModel().getFullModelRange());
}");
        await editor.PressAsync("Delete");
        await page.WaitForFunctionAsync("() => window.trydotnetEditor.editor._editor.getValue() === ''");
    }

    public static async Task<List<JsonElement>> RequestRunAsync(this IPage page, MessageInterceptor interceptor, TimeSpan? delayStart = null)
    {
        await Task.Delay(delayStart ?? TimeSpan.FromSeconds(10));
        var awaiter = interceptor.AwaitForMessage("RunCompleted", TimeSpan.FromMinutes(10));
        await page.DispatchMessage(new
        {
            type = "run"

        });
        await awaiter;
        return interceptor.Messages;
    }

    public static async Task SetCodeUsingTryDotNetJsApi(this IPage page, MessageInterceptor interceptor, string code, TimeSpan? delayStart = null)
    {
        await page.RunAndWaitForConsoleMessageAsync(async () =>
        {
            var dotnetOnline = new DotNetOnline(page);
            var documentOpenAwaiter = interceptor.AwaitForMessage("DocumentOpened");
            await dotnetOnline.SetCodeAsync(code);
            await documentOpenAwaiter;
        },
            new PageRunAndWaitForConsoleMessageOptions
            {
                Predicate = message => message.Text.Contains($"[trydotnet-editor] [MonacoEditorArapter.setCode]: {code}"),
                Timeout = Debugger.IsAttached ? 0.0f : (float)TimeSpan.FromMinutes(10).TotalMilliseconds
            }
        );
    }


    public static async Task<List<WasmRunnerMessage>> ExecuteAssembly(this IPage page, string base64EncodedAssembly)
    {
        var messages = new List<WasmRunnerMessage>();
        var ts = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await page.ExposeFunctionAsync("postMessageLogger", (JsonElement message) =>
        {
            var wm = message.Deserialize<WasmRunnerMessage>()!;
            messages.Add(wm);

            if (wm.type == "wasmRunner-result")
            {
                ts.SetResult();
            }
        });

        await page.DispatchMessage(new
        {
            type = "wasmRunner-command",
            base64EncodedAssembly
        });

        await ts.Task.Timeout(TimeSpan.FromSeconds(60),"Timeout waiting for wasmRunner-result");

        return messages;

    }
}