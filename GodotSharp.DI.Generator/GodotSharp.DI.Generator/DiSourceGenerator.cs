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

        var cached = new CachedSymbol(compilation);

        // 收集所有类型符号
        var allTypes = classDecls
            .Select(cls => compilation.GetSemanticModel(cls.SyntaxTree).GetDeclaredSymbol(cls))
            .OfType<INamedTypeSymbol>()
            .ToArray();

        // 分类
        var hosts = new List<INamedTypeSymbol>();
        var users = new List<INamedTypeSymbol>();
        var scopes = new List<INamedTypeSymbol>();

        foreach (var type in allTypes)
        {
            var isHost = SymbolHelper.ImplementsInterface(type, cached.ServiceHostInterface);
            var isUser = SymbolHelper.ImplementsInterface(type, cached.ServiceUserInterface);
            var isScope = SymbolHelper.ImplementsInterface(type, cached.ServiceScopeInterface);
            if (isHost || isUser)
            {
                if (isScope)
                {
                    continue;
                }
                var utilsSource = CodeWriter.GenerateHostAndUserUtilsCode(
                    type,
                    cached,
                    isHost,
                    isUser
                );
                if (!string.IsNullOrWhiteSpace(utilsSource))
                {
                    context.AddSource($"{type.Name}.ServiceUtils.g.cs", utilsSource);
                }
            }

            if (SymbolHelper.ImplementsInterface(type, cached.ServiceHostInterface))
                hosts.Add(type);
            if (SymbolHelper.ImplementsInterface(type, cached.ServiceUserInterface))
                users.Add(type);
            if (SymbolHelper.ImplementsInterface(type, cached.ServiceScopeInterface))
                scopes.Add(type);
        }

        foreach (var host in hosts)
        {
            var source = CodeWriter.GenerateHostCode(host, cached);
            context.AddSource($"{host.Name}.ServiceHost.g.cs", source);
        }
        foreach (var user in users)
        {
            var source = CodeWriter.GenerateUserCode(user, cached);
            context.AddSource($"{user.Name}.ServiceUser.g.cs", source);
        }
        foreach (var scope in scopes)
        {
            var source = CodeWriter.GenerateScopeCode(scope, cached);
            context.AddSource($"{scope.Name}.ServiceScope.g.cs", source);

            var utils = CodeWriter.GenerateScopeUtilsCode(scope, cached);
            if (!string.IsNullOrWhiteSpace(utils))
                context.AddSource($"{scope.Name}.ServiceUtils.g.cs", utils);
        }
    }
}
