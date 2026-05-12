using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Internal.Data;
using GodotSharpDI.SourceGenerator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharpDI.SourceGenerator.Internal.Semantic;

internal static class RawClassSemanticInfoFactory
{
    public static (
        RawClassSemanticInfo? Info,
        ImmutableArray<Diagnostic> Diagnostics
    ) CreateWithDiagnostics(Compilation compilation, ClassDeclarationSyntax syntax)
    {
        return CreateWithDiagnostics(compilation, syntax, new CachedSymbols(compilation));
    }

    public static (
        RawClassSemanticInfo? Info,
        ImmutableArray<Diagnostic> Diagnostics
    ) CreateWithDiagnostics(Compilation compilation, ClassDeclarationSyntax syntax, CachedSymbols symbols)
    {
        var model = compilation.GetSemanticModel(syntax.SyntaxTree);
        var declaredSymbol = ModelExtensions.GetDeclaredSymbol(model, syntax);

        if (declaredSymbol is not INamedTypeSymbol symbol)
            return (null, ImmutableArray<Diagnostic>.Empty);

        // 检查是否有相关特性
        var hasHost = symbol.HasAttribute(symbols.HostAttribute);
        var hasUser = symbol.HasAttribute(symbols.UserAttribute);
        var hasModules = symbol.HasAttribute(symbols.ModulesAttribute);

        var implementsIScope = symbols.ImplementsIScope(symbol);
        var implementsIDependenciesResolved = symbols.ImplementsIDependenciesResolved(symbol);
        var isNode = symbols.IsNode(symbol);
        var isPartial = syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

        // 如果没有任何 DI 相关特性且没有实现 IScope，跳过
        if (!hasHost && !hasUser && !hasModules && !implementsIScope)
            return (null, ImmutableArray<Diagnostic>.Empty);

        // 收集成员：字段、属性和普通方法
        // 排除：构造函数、属性访问器（get/set）、编译器生成的方法
        var members = symbol
            .GetMembers()
            .Where(m =>
            {
                if (m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property)
                    return true;

                if (m.Kind == SymbolKind.Method && m is IMethodSymbol method)
                {
                    // 排除构造函数、属性访问器和编译器生成的特殊方法
                    return method.MethodKind == MethodKind.Ordinary;
                }

                return false;
            })
            .ToImmutableArray();

        var info = new RawClassSemanticInfo(
            Symbol: symbol,
            Location: syntax.Identifier.GetLocation(),
            HasHostAttribute: hasHost,
            HasUserAttribute: hasUser,
            HasModulesAttribute: hasModules,
            ImplementsIScope: implementsIScope,
            ImplementsIDependenciesResolved: implementsIDependenciesResolved,
            IsNode: isNode,
            IsPartial: isPartial,
            Members: members
        );

        return (info, ImmutableArray<Diagnostic>.Empty);
    }
}
