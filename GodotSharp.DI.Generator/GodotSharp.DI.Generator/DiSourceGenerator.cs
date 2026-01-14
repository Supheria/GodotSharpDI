using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using GodotSharp.DI.Generator.Internal;
using GodotSharp.DI.Generator.Internal.Coding;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GodotSharp.DI.Generator;

[Generator]
public sealed class DiSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. transform 阶段做语义筛选
        var filteredTypes = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (n, _) => n is ClassDeclarationSyntax,
                transform: static (ctx, _) =>
                {
                    var symbol = ctx.SemanticModel.GetDeclaredSymbol(
                        (ClassDeclarationSyntax)ctx.Node
                    );
                    if (symbol is INamedTypeSymbol type)
                    {
                        return SymbolHelper.IsDiRelevant(type) ? type : null;
                    }

                    return null;
                }
            )
            .Where(static t => t is not null);

        // 2. 去重（避免重复处理 partial）
        var distinctTypes = filteredTypes
            .Select(static (t, _) => t!)
            .WithComparer(SymbolEqualityComparer.Default)
            .Collect();

        // 3. CachedSymbol
        var symbolsProvider = context.CompilationProvider.Select(
            static (c, _) => new SymbolCache(c)
        );

        // 4. 构建 ServiceGraph
        var graphProvider = distinctTypes
            .Combine(symbolsProvider)
            .Select(
                static (pair, _) =>
                {
                    var (types, cached) = pair;
                    var builder = new ServiceGraphBuilder();
                    return builder.Build(types, cached);
                }
            );

        // 4. 注册生成器模块
        context.RegisterSourceOutput(graphProvider, Generate);
    }

    private static void Generate(SourceProductionContext context, ServiceGraph graph)
    {
        ServiceFactoryGenerator.Generate(context, graph);
        HostGenerator.Generate(context, graph);
        UserGenerator.Generate(context, graph);
        ScopeGenerator.Generate(context, graph);
    }

    // // 4. 生成代码
    // context.RegisterSourceOutput(
    //     graphProvider,
    //     static (spc, graph) =>
    //     {
    //         var allHostUser = new Dictionary<INamedTypeSymbol, (bool IsHost, bool IsUser)>(
    //             SymbolEqualityComparer.Default
    //         );
    //
    //         foreach (var host in graph.Hosts)
    //         {
    //             var type = host.HostType;
    //             spc.AddSource($"{type.Name}.DI.Host.g.cs", CodeWriter.GenerateHost(host));
    //             if (!allHostUser.ContainsKey(type))
    //             {
    //                 allHostUser[type] = (false, false);
    //             }
    //             allHostUser[type] = allHostUser[type] with { IsHost = true };
    //         }
    //
    //         foreach (var user in graph.Users)
    //         {
    //             var type = user.UserType;
    //             spc.AddSource($"{type.Name}.DI.User.g.cs", CodeWriter.GenerateUser(user));
    //             if (!allHostUser.ContainsKey(type))
    //             {
    //                 allHostUser[type] = (false, false);
    //             }
    //             allHostUser[type] = allHostUser[type] with { IsUser = true };
    //         }
    //
    //         foreach (var pair in allHostUser)
    //         {
    //             var type = pair.Key;
    //             var value = pair.Value;
    //             spc.AddSource(
    //                 $"{type.Name}.DI.g.cs",
    //                 CodeWriter.GenerateHostUserUtils(type, value.IsHost, value.IsUser)
    //             );
    //         }
    //
    //         foreach (var scope in graph.Scopes)
    //         {
    //             spc.AddSource(
    //                 $"{scope.ScopeType.Name}.DI.Scope.g.cs",
    //                 CodeWriter.GenerateScope(scope, graph)
    //             );
    //
    //             spc.AddSource(
    //                 $"{scope.ScopeType.Name}.DI.g.cs",
    //                 CodeWriter.GenerateScopeUtils(scope, graph)
    //             );
    //         }
    //     }
    // );
}
