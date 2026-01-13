using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using GodotSharp.DI.Generator.Internal;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace GodotSharp.DI.Generator;

[Generator]
public sealed class DiSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // 1. 语法筛选：只关心 class 声明
        var classDeclarations = context
            .SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => (ClassDeclarationSyntax)ctx.Node
            )
            .Where(static cls => cls is not null);
        // 2. 收集 Compilation + 所有 class
        var compilationAndClasses = context.CompilationProvider.Combine(
            classDeclarations.Collect()
        );
        // 3. 注册生成
        context.RegisterSourceOutput(compilationAndClasses, Execute);
    }

    private static void Execute(
        SourceProductionContext context,
        (Compilation compilation, ImmutableArray<ClassDeclarationSyntax> classes) input
    )
    {
        var (compilation, classDecls) = input;
        if (classDecls.IsDefaultOrEmpty)
            return;

        var symbol = new CachedSymbol(compilation);

        // 收集所有类型符号
        var allTypes = classDecls
            .Select(cls => compilation.GetSemanticModel(cls.SyntaxTree).GetDeclaredSymbol(cls))
            .OfType<INamedTypeSymbol>()
            .ToArray();

        var registry = new ServiceRegistry();

        foreach (var type in allTypes)
        {
            var isHost = SymbolHelper.ImplementsInterface(type, symbol.ServiceHostInterface);
            var isUser = SymbolHelper.ImplementsInterface(type, symbol.ServiceUserInterface);
            var isScope = SymbolHelper.ImplementsInterface(type, symbol.ServiceScopeInterface);

            var serviceTypeInfo = ServiceTypeCollector.Analyze(type, symbol);
            if (serviceTypeInfo is not null)
            {
                if (!isHost && !isUser && !isScope)
                {
                    registry.Services[type] = serviceTypeInfo;
                }
                continue;
            }

            if (isHost || isUser)
            {
                if (!isScope)
                {
                    var isNode = false;
                    if (isHost)
                    {
                        var info = HostServiceCollector.Analyze(type, symbol);
                        registry.Hosts[type] = info;
                        isNode = info.IsNode;
                    }
                    if (isUser)
                    {
                        var info = UserDependencyCollector.Analyze(type, symbol);
                        registry.Users[type] = info;
                        isNode = info.IsNode;
                    }
                    if (isNode)
                    {
                        var utils = CodeWriter.GenerateUtilsCode(type, isHost, isUser);
                        // context.AddSource($"{type.Name}.ServiceUtils.g.cs", utils);
                    }
                }
                continue;
            }

            if (isScope)
            {
                registry.Scopes[type] = ScopeServiceCollector.Analyze(type, symbol);
            }
        }

        foreach (var host in registry.Hosts.Values)
        {
            if (!host.IsNode)
            {
                continue;
            }
            var source = CodeWriter.GenerateHostCode(host);
            context.AddSource($"{host.HostType.Name}.ServiceHost.g.cs", source);
        }

        foreach (var user in registry.Users.Values)
        {
            if (!user.IsNode)
            {
                continue;
            }
            var source = CodeWriter.GenerateUserCode(user);
            context.AddSource($"{user.UserType.Name}.ServiceUser.g.cs", source);
        }

        foreach (var scope in registry.Scopes.Values)
        {
            if (!scope.IsNode)
            {
                continue;
            }
            var source = CodeWriter.GenerateScopeCode(scope, registry);
            context.AddSource($"{scope.ScopeType.Name}.ServiceScope.g.cs", source);

            var utils = CodeWriter.GenerateUtilsCode(scope, registry);
            context.AddSource($"{scope.ScopeType.Name}.ServiceUtils.g.cs", utils);
        }
    }
}
