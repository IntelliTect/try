using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using AwesomeAssertions.Execution;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.DotNet.Interactive.Connection;
using Microsoft.DotNet.Interactive.CSharpProject.Events;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.TryDotNet.WasmRunner;
using Pocket;
using Pocket.For.Xunit;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.TryDotNet.Tests;

[LogToPocketLogger(FileNameEnvironmentVariable = "POCKETLOGGER_LOG_PATH")]
public class CommandExecutionTests : IDisposable
{
    private readonly CompositeDisposable _disposables = new();

    public CommandExecutionTests(ITestOutputHelper output)
    {
        _disposables.Add(output.SubscribeToPocketLogger());
    }

    [Fact]
    public async Task can_compile_projects_with_user_code_in_region()
    {
        await using var applicationBuilderFactory = new WebApplicationFactory<Program>();

        var c = applicationBuilderFactory.CreateDefaultClient();

        var requests = """
            {
                "commands": [
                    {
                        "commandType": "OpenProject",
                        "command": {
                            "project": {
                                "files": [
                                    {
                                        "relativeFilePath": "program.cs",
                                        "content": "using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Text;\nusing System.Globalization;\nusing System.Text.RegularExpressions;\n\nnamespace Program\n{\n  class Program\n  {\n    static void Main(string[] args)\n    {\n      #region controller\n\n      #endregion\n    }\n  }\n}"
                                    }
                                ]
                            }
                        },
                        "token": "595d327c-b14f-5ad7-7da0-2579cbfa9961::22||6"
                    },
                    {
                        "commandType": "OpenDocument",
                        "command": {
                            "relativeFilePath": "./program.cs",
                            "regionName": "controller"
                        },
                        "token": "595d327c-b14f-5ad7-7da0-2579cbfa9961::22||7"
                    },
                    {
                        "commandType": "SubmitCode",
                        "command": {
                            "code": "var a = 123;"
                        },
                        "token": "595d327c-b14f-5ad7-7da0-2579cbfa9961::22||8"
                    },
                    {
                        "commandType": "CompileProject",
                        "command": {},
                        "token": "595d327c-b14f-5ad7-7da0-2579cbfa9961::22"
                    }
                ]
            }
            """;

        var requestBody = JsonContent.Create(JsonDocument.Parse(requests).RootElement);

        var response = await c.PostAsync("commands", requestBody);

        var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None)).RootElement;

        var events = responseJson.GetProperty("events").EnumerateArray().Select(KernelEventEnvelope.Deserialize).Select(ee => ee.Event).ToList();

        using var _ = new AssertionScope();

        response.EnsureSuccessStatusCode();

        var assemblyProduced = events.OfType<AssemblyProduced>().SingleOrDefault();
        assemblyProduced.Should().NotBeNull();
        assemblyProduced!.Assembly.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData("""
        using System;
        using System.Collections.Generic;

        class Program
        {
            static void Main()
            {
                HashSet<string> values = [with(StringComparer.OrdinalIgnoreCase), "Hello", "HELLO", "world"];
                Console.WriteLine(values.Count);
            }
        }
        """, "2")]
    [InlineData("""
        using System;

        class Program
        {
            static void Main()
            {
                NumberOrText value = new Number(42);
                Console.WriteLine(value switch { Number(var number) => number.ToString(), Text(var text) => text });
            }
        }

        public record Number(int Value);
        public record Text(string Value);
        public union NumberOrText(Number, Text);
        """, "42")]
    [InlineData("""
        using System;

        class Program
        {
            static void Main()
            {
                State state = new Open(42);
                Console.WriteLine(state switch { Open(var value) => value.ToString(), Closed => "closed" });
            }
        }

        public closed record class State;
        public record class Open(int Value) : State;
        public record class Closed : State;
        """, "42")]
    [InlineData("""
        using System;
        using System.Collections.Generic;
        using System.Linq;

        class Program
        {
            static void Main()
            {
                IEnumerable<int> values = new[] { 1, 2, 3 };
                Console.WriteLine(values[1]);
            }
        }

        public static class SequenceExtensions
        {
            extension(IEnumerable<int> sequence)
            {
                public int this[int index] => sequence.ElementAt(index);
            }
        }
        """, "2")]
    [InlineData("""
        using System;

        class Program
        {
            static void Main()
            {
                var count = 0;
                outer: for (var row = 0; row < 3; row++)
                {
                    for (var column = 0; column < 3; column++)
                    {
                        count++;
                        continue outer;
                    }
                }
                Console.WriteLine(count);
            }
        }
        """, "3")]
    public async Task can_compile_and_run_csharp15_code(string code, string expectedOutput)
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateDefaultClient();
        var escapedCode = JsonEncodedText.Encode(code).ToString();
        var requests = $$"""
            {
                "commands": [
                    {
                        "commandType": "OpenProject",
                        "command": {
                            "project": {
                                "files": [
                                    {
                                        "relativeFilePath": "program.cs",
                                        "content": "{{escapedCode}}"
                                    }
                                ]
                            }
                        },
                        "token": "csharp15::1"
                    },
                    {
                        "commandType": "OpenDocument",
                        "command": { "relativeFilePath": "program.cs" },
                        "token": "csharp15::2"
                    },
                    {
                        "commandType": "CompileProject",
                        "command": {},
                        "token": "csharp15::3"
                    }
                ]
            }
            """;
        using var requestBody = JsonContent.Create(JsonDocument.Parse(requests).RootElement);
        using var response = await client.PostAsync("commands", requestBody);
        response.EnsureSuccessStatusCode();

        using var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var events = responseJson.RootElement.GetProperty("events")
            .EnumerateArray()
            .Select(KernelEventEnvelope.Deserialize)
            .Select(envelope => envelope.Event)
            .ToList();

        var diagnostics = string.Join(Environment.NewLine,
            events.OfType<DiagnosticsProduced>().SelectMany(produced => produced.Diagnostics));
        events.OfType<CommandFailed>().Should().BeEmpty(diagnostics);
        var assembly = events.OfType<AssemblyProduced>().Should().ContainSingle().Which.Assembly.Value;
        var output = new List<string>();
        var errors = new List<string>();
        var result = await new CodeRunner().RunAssemblyEntryPoint(assembly, output.Add, errors.Add);

        result.Succeeded.Should().BeTrue((result.Exception ?? result.RunnerException)?.ToString());
        errors.Should().BeEmpty();
        output.Select(line => line.Trim()).Should().Equal(expectedOutput);
    }

    [Fact]
    public async Task can_open_document_with_user_code_in_region()
    {
        await using var applicationBuilderFactory = new WebApplicationFactory<Program>();

        var c = applicationBuilderFactory.CreateDefaultClient();

        var requests = """
            {
                "commands": [
                    {
                        "commandType": "OpenProject",
                        "command": {
                            "project": {
                                "files": [
                                    {
                                        "relativeFilePath": "program.cs",
                                        "content": "using System;\nusing System.Collections.Generic;\nusing System.Linq;\nusing System.Text;\nusing System.Globalization;\nusing System.Text.RegularExpressions;\n\nnamespace Program\n{\n  class Program\n  {\n    static void Main(string[] args)\n    {\n      #region controller\nConsole.WriteLine(123);\n      #endregion\n    }\n  }\n}"
                                    }
                                ]
                            }
                        },
                        "token": "595d327c-b14f-5ad7-7da0-2579cbfa9961::22||6"
                    },
                    {
                        "commandType": "OpenDocument",
                        "command": {
                            "relativeFilePath": "program.cs",
                            "regionName": "controller"
                        },
                        "token": "595d327c-b14f-5ad7-7da0-2579cbfa9961::22||7"
                    }
                ]
            }
            """;

        var requestBody = JsonContent.Create(JsonDocument.Parse(requests).RootElement);

        var response = await c.PostAsync("commands", requestBody);

        var responseJson = JsonDocument.Parse(await response.Content.ReadAsStringAsync(CancellationToken.None)).RootElement;

        var events = responseJson.GetProperty("events").EnumerateArray().Select(KernelEventEnvelope.Deserialize).Select(ee => ee.Event).ToList();

        using var _ = new AssertionScope();

        response.EnsureSuccessStatusCode();

        var documentOpened = events.OfType<DocumentOpened>().SingleOrDefault();
        documentOpened.Should().NotBeNull();
        documentOpened!.Content.Should().Contain("Console.WriteLine(123);");
    }

    public void Dispose() => _disposables.Dispose();
}