using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace GodotSharp.DI.Generator.Internal.Semantic;

internal static class RawClassSemanticInfoFactory
{
    public static (
        RawClassSemanticInfo? Info,
        ImmutableArray<Diagnostic> Diagnostics
    ) CreateWithDiagnostics(Compilation compilation, ClassDeclarationSyntax syntax)
    {
        var model = compilation.GetSemanticModel(syntax.SyntaxTree);
        var declaredSymbol = model.GetDeclaredSymbol(syntax);

        if (declaredSymbol is not INamedTypeSymbol symbol)
            return (null, ImmutableArray<Diagnostic>.Empty);

        var symbols = new CachedSymbols(compilation);

        // 检查是否有相关特性
        var hasSingleton = HasAttribute(symbol, symbols.SingletonAttribute);
        var hasTransient = HasAttribute(symbol, symbols.TransientAttribute);
        var hasHost = HasAttribute(symbol, symbols.HostAttribute);
        var hasUser = HasAttribute(symbol, symbols.UserAttribute);
        var hasModules = HasAttribute(symbol, symbols.ModulesAttribute);

        var implementsIScope = symbols.ImplementsIScope(symbol);
        var implementsIServicesReady = symbols.ImplementsIServicesReady(symbol);
        var isNode = symbols.IsNode(symbol);
        var isPartial = syntax.Modifiers.Any(m =>
            m.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword)
        );

        // 如果没有任何 DI 相关特性且没有实现 IScope，跳过
        if (
            !hasSingleton
            && !hasTransient
            && !hasHost
            && !hasUser
            && !hasModules
            && !implementsIScope
        )
            return (null, ImmutableArray<Diagnostic>.Empty);

        var members = symbol
            .GetMembers()
            .Where(m => m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property)
            .ToImmutableArray();

        var constructors = symbol.Constructors.Where(c => !c.IsStatic).ToImmutableArray();

        var info = new RawClassSemanticInfo(
            Symbol: symbol,
            Location: syntax.GetLocation(),
            HasSingletonAttribute: hasSingleton,
            HasTransientAttribute: hasTransient,
            HasHostAttribute: hasHost,
            HasUserAttribute: hasUser,
            HasModulesAttribute: hasModules,
            ImplementsIScope: implementsIScope,
            ImplementsIServicesReady: implementsIServicesReady,
            IsNode: isNode,
            IsPartial: isPartial,
            Members: members,
            Constructors: constructors
        );

        return (info, ImmutableArray<Diagnostic>.Empty);
    }

    private static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return false;

        return symbol
            .GetAttributes()
            .Any(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeSymbol)
            );
    }
}
