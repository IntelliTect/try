using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.Interactive;
using Microsoft.DotNet.Interactive.Commands;
using Microsoft.DotNet.Interactive.CSharpProject;
using Microsoft.DotNet.Interactive.CSharpProject.Build;
using Microsoft.DotNet.Interactive.CSharpProject.Commands;
using Microsoft.DotNet.Interactive.CSharpProject.Events;
using Microsoft.DotNet.Interactive.Events;
using Microsoft.DotNet.Interactive.Formatting;
using ProjectWorkspace = Microsoft.DotNet.Interactive.CSharpProject.Workspace;
using ProjectBuffer = Microsoft.DotNet.Interactive.CSharpProject.Buffer;
using KernelDiagnostic = Microsoft.DotNet.Interactive.Diagnostic;
using KernelLinePosition = Microsoft.DotNet.Interactive.LinePosition;
using KernelLinePositionSpan = Microsoft.DotNet.Interactive.LinePositionSpan;

namespace Microsoft.TryDotNet;

internal sealed class ProjectCompiler(Prebuild prebuild)
{
    private ProjectWorkspace? _workspace;
    private ProjectBuffer? _buffer;

    public void Attach(CSharpProjectKernel kernel)
    {
        kernel.AddMiddleware(async (command, context, next) =>
        {
            await next(command, context);

            switch (command)
            {
                case OpenProject open:
                    _workspace = new BufferFromRegionExtractor().Extract(open.Project.Files
                        .Select(file => new ProjectFileContent(file.RelativeFilePath, file.Content)).ToArray());
                    _buffer = null;
                    break;
                case OpenDocument open when _workspace is not null:
                    var file = _workspace.Files.SingleOrDefault(file =>
                        string.Equals(file.Name, open.RelativeFilePath, StringComparison.OrdinalIgnoreCase));
                    _buffer = string.IsNullOrWhiteSpace(open.RegionName)
                        ? new ProjectBuffer(open.RelativeFilePath, file?.Text ?? "")
                        : _workspace.Buffers.Single(buffer =>
                            string.Equals(buffer.Id.FileName, open.RelativeFilePath, StringComparison.OrdinalIgnoreCase) &&
                            buffer.Id.RegionName == open.RegionName);
                    break;
                case SubmitCode submit when _workspace is not null && _buffer is not null:
                    _workspace = await new ProjectWorkspace(files: _workspace.Files,
                        buffers: _workspace.Buffers.Where(buffer => buffer.Id != _buffer.Id)
                            .Append(new ProjectBuffer(_buffer.Id, submit.Code)).ToArray()).InlineBuffersAsync();
                    _buffer = _workspace.Buffers.Single(buffer => buffer.Id == _buffer.Id);
                    break;
            }
        });
        kernel.RegisterCommandHandler<CompileProject>(CompileAsync);
    }

    private async Task CompileAsync(CompileProject command, KernelInvocationContext context)
    {
        if (_workspace is null || _buffer is null)
        {
            context.Fail(command, message: "Open a project and document before compiling.");
            return;
        }

        var sourceWorkspace = await _workspace.InlineBuffersAsync();
        using var workspace = await prebuild.CreateWorkspaceAsync();
        var project = workspace.CurrentSolution.Projects.Single();
        var intermediateDirectory = Path.Combine(prebuild.Directory.FullName, "obj");
        // Template assembly metadata must not conflict with attributes authored in submitted files.
        foreach (var document in project.Documents.Where(document =>
                     document.FilePath is { } path &&
                     path.StartsWith(intermediateDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) &&
                     Path.GetFileName(path).EndsWith(".AssemblyInfo.cs", StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            project = project.RemoveDocument(document.Id);
        }

        if (Directory.Exists(intermediateDirectory))
        {
            // The prebuild cache drops generated sources on Windows, including SDK implicit usings.
            foreach (var path in Directory.EnumerateFiles(intermediateDirectory, "*.GlobalUsings.g.cs", SearchOption.AllDirectories))
            {
                if (!project.Documents.Any(document => string.Equals(document.FilePath, path, StringComparison.OrdinalIgnoreCase)))
                {
                    project = project.AddDocument(Path.GetFileName(path), await File.ReadAllTextAsync(path), filePath: path).Project;
                }
            }
        }

        foreach (var source in sourceWorkspace.GetSourceFiles())
        {
            var sourcePath = Path.GetFullPath(source.Name, prebuild.Directory.FullName);
            var document = project.Documents.FirstOrDefault(document =>
                string.Equals(document.FilePath, sourcePath, StringComparison.OrdinalIgnoreCase));
            project = document is null
                ? project.AddDocument(source.Name, source.Text, filePath: sourcePath).Project
                : document.WithText(source.Text).Project;
        }

        var compilation = (CSharpCompilation)(await project.GetCompilationAsync())!;
        if (compilation.GetDiagnostics().Any(diagnostic => diagnostic.Id == "CS5001"))
        {
            // Type-only listings are libraries; missing an entry point must not hide other compiler errors.
            compilation = compilation.WithOptions(compilation.Options.WithOutputKind(OutputKind.DynamicallyLinkedLibrary));
        }

        using var assembly = new MemoryStream();
        var result = compilation.Emit(assembly);
        var activeBuffer = sourceWorkspace.Buffers.SingleOrDefault(buffer => buffer.Id == _buffer.Id) ?? _buffer;
        var activePath = Path.GetFullPath(activeBuffer.Id.FileName, prebuild.Directory.FullName);
        var offset = activeBuffer.AbsolutePosition - activeBuffer.Position;
        var activeText = Microsoft.CodeAnalysis.Text.SourceText.From(activeBuffer.Content);
        var diagnostics = result.Diagnostics.Where(diagnostic =>
                diagnostic.Location.IsInSource &&
                string.Equals(diagnostic.Location.SourceTree?.FilePath, activePath, StringComparison.OrdinalIgnoreCase) &&
                diagnostic.Location.SourceSpan.Start >= offset &&
                diagnostic.Location.SourceSpan.End <= offset + activeText.Length)
            .Select(diagnostic =>
        {
            var span = activeText.Lines.GetLinePositionSpan(new Microsoft.CodeAnalysis.Text.TextSpan(
                diagnostic.Location.SourceSpan.Start - offset, diagnostic.Location.SourceSpan.Length));
            return new KernelDiagnostic(new KernelLinePositionSpan(
                    new KernelLinePosition(span.Start.Line, span.Start.Character),
                    new KernelLinePosition(span.End.Line, span.End.Character)),
                diagnostic.Severity, diagnostic.Id, diagnostic.GetMessage());
        }).ToArray();
        context.Publish(new DiagnosticsProduced(diagnostics,
            diagnostics.Select(diagnostic => new FormattedValue("text/plain", diagnostic.ToString())).ToArray(), command));

        if (!result.Success || assembly.Length == 0)
        {
            context.Fail(command, message: string.Join(Environment.NewLine,
                result.Diagnostics.Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)));
            return;
        }

        context.Publish(new AssemblyProduced(command, new Base64EncodedAssembly(Convert.ToBase64String(assembly.ToArray()))));
    }
}
