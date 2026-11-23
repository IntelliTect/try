using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Microsoft.TryDotNet.FileIntegration.Tests;

internal static class ListingSourceNormalizer
{
    public static string RemoveRegions(string source)
    {
        var tree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var regions = tree.GetRoot().DescendantTrivia(descendIntoTrivia: true)
            .Where(trivia => trivia.IsKind(SyntaxKind.RegionDirectiveTrivia) ||
                             trivia.IsKind(SyntaxKind.EndRegionDirectiveTrivia) ||
                             trivia.GetStructure() is BadDirectiveTriviaSyntax { Identifier.ValueText: "endregion" });
        var normalized = source.ToCharArray();

        foreach (var region in regions)
        {
            for (var index = region.FullSpan.Start; index < region.FullSpan.End; index++)
            {
                if (normalized[index] is not '\r' and not '\n')
                {
                    normalized[index] = ' ';
                }
            }
        }

        return new string(normalized);
    }
}
