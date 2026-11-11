// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using Microsoft.JSInterop;

namespace Microsoft.TryDotNet.WasmRunner;

public class CodeRunnerAdapter
{
    private readonly CodeRunner _runner;
    private readonly IJSRuntime _jsInterop;

    public CodeRunnerAdapter(CodeRunner runner, IJSRuntime jsInterop)
    {
        _runner = runner;
        _jsInterop = jsInterop;
    }

    [JSInvokable]
    public async Task<SerializableCodeRunnerResult> RunAssembly(string base64EncodedAssembly)
    {
        var pendingOutput = new ConcurrentQueue<Task<JSException?>>();
        var result = await _runner.RunAssemblyEntryPoint(base64EncodedAssembly,
            output => pendingOutput.Enqueue(PublishOutputAsync("publishCodeRunnerStdOut", output)),
            error => pendingOutput.Enqueue(PublishOutputAsync("publishCodeRunnerStdError", error)));

        var outputErrors = await Task.WhenAll(pendingOutput);
        var outputError = outputErrors.FirstOrDefault(exception => exception is not null);

        return new SerializableCodeRunnerResult(
            result.Succeeded && outputError is null,
            result.Exception?.ToString(),
            outputError?.ToString() ?? result.RunnerException?.ToString());
    }

    private async Task<JSException?> PublishOutputAsync(string identifier, string output)
    {
        try
        {
            await _jsInterop.InvokeVoidAsync(identifier, output);
            return null;
        }
        catch (JSException exception)
        {
            return exception;
        }
    }
}

public record SerializableCodeRunnerResult(bool Success, string? Error, string? RunnerError);