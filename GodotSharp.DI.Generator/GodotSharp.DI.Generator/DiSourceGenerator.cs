using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using GodotSharp.DI.Generator.Internal;
using GodotSharp.DI.Generator.Internal.Coding;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.DiBuild;
using GodotSharp.DI.Generator.Internal.Helpers;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GodotSharp.DI.Generator;

[Generator]
public sealed class DiSourceGenerator : IIncrementalGenerator
{
    private sealed class ClassTypeComparer : IEqualityComparer<ClassType>
    {
        public static readonly ClassTypeComparer Default = new();

        private ClassTypeComparer() { }

        public bool Equals(ClassType? x, ClassType? y)
        {
            if (ReferenceEquals(x, y))
            {
                return true;
            }
            if (x is null || y is null)
            {
                return false;
            }
            return SymbolEqualityComparer.Default.Equals(x.Symbol, y.Symbol);
        }

        public int GetHashCode(ClassType obj)
        {
            return SymbolEqualityComparer.Default.GetHashCode(obj.Symbol);
        }
    }

    private static ClassType? TypeFilter(GeneratorSyntaxContext context, CancellationToken _)
    {
        try
        {
            var classDecl = (ClassDeclarationSyntax)context.Node;
            var symbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
            if (symbol is INamedTypeSymbol type && TypeHelper.IsDiRelevant(type))
            {
                return new ClassType(type, classDecl);
            }
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static DiGraphBuildResult BuildDiGraph(
        (ImmutableArray<ClassType>, CachedSymbols) pair,
        CancellationToken ct
    )
    {
        try
        {
            ct.ThrowIfCancellationRequested();
        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(
                descriptor: DiagnosticDescriptors.RequestCancellation,
                location: Location.None,
                ex.Message
            );
            return DiGraphBuildResult.Failure(ImmutableArray.Create(diagnostic));
        }

        try
        {
            var (types, symbols) = pair;
            var builder = new DiGraphBuilder(types, symbols);
            return builder.Build();
        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(
                descriptor: DiagnosticDescriptors.GeneratorInternalError,
                location: Location.None,
                ex.Message
            );
            return DiGraphBuildResult.Failure(ImmutableArray.Create(diagnostic));
        }
    }

    void IIncrementalGenerator.Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. transform 阶段做语义筛选
        var filteredTypes = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) =>
                    node is ClassDeclarationSyntax classDecl && classDecl.AttributeLists.Count > 0,
                transform: TypeFilter
            )
            .Where(static t => t is not null)
            .Select(static (t, _) => t!);

        // 2. 去重（避免重复处理 partial）
        var distinctTypes = filteredTypes.WithComparer(ClassTypeComparer.Default).Collect();

        // 3. CachedSymbol
        var symbolsProvider = context.CompilationProvider.Select(
            static (c, _) => new CachedSymbols(c)
        );

        // 4. 构建 DiGraph
        var graphProvider = distinctTypes.Combine(symbolsProvider).Select(BuildDiGraph);

        // 5. 注册生成器模块
        context.RegisterSourceOutput(
            graphProvider,
            static (spc, result) =>
            {
                foreach (var diagnostic in result.Diagnostics)
                {
                    spc.ReportDiagnostic(diagnostic);
                }
                if (result.Graph is not null)
                {
                    GenerateAllSources(spc, result.Graph);
                }
            }
        );
    }

    private static void GenerateAllSources(SourceProductionContext context, DiGraph graph)
    {
        try
        {
            ServiceGenerator.Generate(context, graph);
            HostGenerator.Generate(context, graph);
            UserGenerator.Generate(context, graph);
            ScopeGenerator.Generate(context, graph);
        }
        catch (Exception ex)
        {
            var diagnostic = Diagnostic.Create(
                descriptor: DiagnosticDescriptors.GeneratorInternalError,
                location: Location.None,
                ex.Message
            );
            context.ReportDiagnostic(diagnostic);
        }
    }
}
