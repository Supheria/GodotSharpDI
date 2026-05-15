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

        // Check for relevant attributes
        var hasHost = symbol.HasAttribute(symbols.HostAttribute);
        var hasUser = symbol.HasAttribute(symbols.UserAttribute);
        var hasModules = symbol.HasAttribute(symbols.ModulesAttribute);

        var implementsIScope = symbols.ImplementsIScope(symbol);
        var implementsIDependenciesResolved = symbols.ImplementsIDependenciesResolved(symbol);
        var isNode = symbols.IsNode(symbol);
        var isPartial = syntax.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword));

        // Skip if no DI-related attributes and does not implement IScope
        if (!hasHost && !hasUser && !hasModules && !implementsIScope)
            return (null, ImmutableArray<Diagnostic>.Empty);

        // Collect members: fields, properties, and ordinary methods
        // Exclude: constructors, property accessors (get/set), compiler-generated methods
        var members = symbol
            .GetMembers()
            .Where(m =>
            {
                if (m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property)
                    return true;

                if (m.Kind == SymbolKind.Method && m is IMethodSymbol method)
                {
                    // Exclude constructors, property accessors, and compiler-generated special methods
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
