using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using GodotSharp.DI.Generator.Internal;
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
        // 1. CachedSymbol（仅用于最终阶段）
        var symbolsProvider = context.CompilationProvider.Select(
            static (c, _) => new CachedSymbol(c)
        );

        // 2. transform 阶段做语义筛选（不依赖 CachedSymbol）
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
            .Where(static t => t is not null)!
            .Collect();

        // 3. 构建 DI 图（此时才使用 CachedSymbol）
        var graphProvider = filteredTypes
            .Combine(symbolsProvider)
            .Select(
                static (pair, _) =>
                {
                    var (types, cached) = pair;
                    return ServiceGraphBuilder.Build(types!, cached);
                }
            );

        // 4. 生成代码
        context.RegisterSourceOutput(
            graphProvider,
            static (spc, graph) =>
            {
                var allHostUser = new Dictionary<INamedTypeSymbol, (bool IsHost, bool IsUser)>(
                    SymbolEqualityComparer.Default
                );

                foreach (var host in graph.Hosts)
                {
                    var type = host.HostType;
                    spc.AddSource($"{type.Name}.ServiceHost.g.cs", CodeWriter.GenerateHost(host));
                    if (!allHostUser.ContainsKey(type))
                    {
                        allHostUser[type] = (false, false);
                    }
                    allHostUser[type] = allHostUser[type] with { IsHost = true };
                }

                foreach (var user in graph.Users)
                {
                    var type = user.UserType;
                    spc.AddSource($"{type.Name}.ServiceUser.g.cs", CodeWriter.GenerateUser(user));
                    if (!allHostUser.ContainsKey(type))
                    {
                        allHostUser[type] = (false, false);
                    }
                    allHostUser[type] = allHostUser[type] with { IsUser = true };
                }

                foreach (var pair in allHostUser)
                {
                    var type = pair.Key;
                    var value = pair.Value;
                    spc.AddSource(
                        $"{type.Name}.ServiceUtils.g.cs",
                        CodeWriter.GenerateHostUserUtils(type, value.IsHost, value.IsUser)
                    );
                }

                foreach (var scope in graph.Scopes)
                {
                    spc.AddSource(
                        $"{scope.ScopeType.Name}.ServiceScope.g.cs",
                        CodeWriter.GenerateScope(scope, graph)
                    );

                    spc.AddSource(
                        $"{scope.ScopeType.Name}.ServiceUtils.g.cs",
                        CodeWriter.GenerateScopeUtils(scope, graph)
                    );
                }
            }
        );
    }
}
