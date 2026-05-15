using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.DiBuild;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using GodotSharpDI.SourceGenerator.Internal.Semantic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharpDI.SourceGenerator.Tests.Helpers;

/// <summary>
/// Shared helper for building DI graphs from source code in tests.
/// Eliminates duplicate BuildGraph patterns across test files.
/// </summary>
internal static class GraphBuildHelper
{
    /// <summary>
    /// Build a DiGraph from source code. Returns the graph build result
    /// containing the graph (or null) and any diagnostics.
    /// </summary>
    public static DiGraphBuildResult BuildGraph(string source)
    {
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        return BuildGraphFromCompilation(compilation);
    }

    /// <summary>
    /// Build a DiGraph from an existing compilation instance.
    /// Use this when you need the compilation for symbol lookups.
    /// </summary>
    public static DiGraphBuildResult BuildGraphFromCompilation(Compilation compilation)
    {
        var symbols = new CachedSymbols(compilation);
        var classResults = CollectValidationResults(compilation, symbols);
        return DiGraphBuilder.Build(classResults.ToImmutable(), symbols);
    }

    /// <summary>
    /// Build a DiGraph and merge class-level + graph-level diagnostics into a single array.
    /// Useful when asserting on diagnostics from multiple pipeline stages.
    /// </summary>
    public static ImmutableArray<Diagnostic> BuildAllDiagnostics(string source)
    {
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        var symbols = new CachedSymbols(compilation);
        var classResults = CollectValidationResults(compilation, symbols);
        var graphResult = DiGraphBuilder.Build(classResults.ToImmutable(), symbols);

        return classResults
            .SelectMany(r => r.Diagnostics)
            .Concat(graphResult.Diagnostics)
            .ToImmutableArray();
    }

    /// <summary>
    /// Build ServiceIndexes from source code.
    /// </summary>
    public static ServiceIndexes BuildServiceIndexes(string source)
    {
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        return BuildServiceIndexesFromCompilation(compilation);
    }

    /// <summary>
    /// Build ServiceIndexes and return the compilation for symbol lookups.
    /// </summary>
    public static (ServiceIndexes Indexes, Compilation Compilation) BuildServiceIndexesWithCompilation(
        string source)
    {
        var compilation = TestCompilationHelper.CreateCompilationWithDI(source);
        return (BuildServiceIndexesFromCompilation(compilation), compilation);
    }

    private static ServiceIndexes BuildServiceIndexesFromCompilation(Compilation compilation)
    {
        var symbols = new CachedSymbols(compilation);
        var hosts = ImmutableArray.CreateBuilder<ValidatedTypeInfo>();
        var users = ImmutableArray.CreateBuilder<ValidatedTypeInfo>();

        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
                if (raw.Info == null)
                    continue;

                var result = ClassPipeline.ValidateAndClassify(raw.Info, symbols);
                if (result.TypeInfo == null)
                    continue;

                if (result.TypeInfo.Role == TypeRole.Host)
                    hosts.Add(result.TypeInfo);
                else if (result.TypeInfo.Role == TypeRole.User)
                    users.Add(result.TypeInfo);
            }
        }

        var diagBuilder = ImmutableArray.CreateBuilder<Diagnostic>();
        var hostNodes = NodeBuilders.BuildHostNodes(hosts.ToImmutable(), diagBuilder);
        var userNodes = NodeBuilders.BuildUserNodes(users.ToImmutable(), diagBuilder);
        return ServiceIndexes.Build(hostNodes, userNodes);
    }

    private static ImmutableArray<ClassValidationResult>.Builder CollectValidationResults(
        Compilation compilation, CachedSymbols symbols)
    {
        var classResults = ImmutableArray.CreateBuilder<ClassValidationResult>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var root = tree.GetRoot();
            foreach (var classDecl in root.DescendantNodes().OfType<ClassDeclarationSyntax>())
            {
                var raw = RawClassSemanticInfoFactory.CreateWithDiagnostics(compilation, classDecl);
                if (raw.Info != null)
                    classResults.Add(ClassPipeline.ValidateAndClassify(raw.Info, symbols));
            }
        }
        return classResults;
    }
}
