using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class TypeFormatter
{
    private static readonly SymbolDisplayFormat ClassNameFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );
    private static readonly SymbolDisplayFormat FullyQualifiedNoAliasFormat = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    public static bool GetNamespace(INamedTypeSymbol type, out string ns)
    {
        if (type.ContainingNamespace.IsGlobalNamespace)
        {
            ns = string.Empty;
            return false;
        }
        ns = type.ContainingNamespace.ToDisplayString();
        return true;
    }

    public static string GetClassName(INamedTypeSymbol type)
    {
        return type.ToDisplayString(ClassNameFormat);
    }

    public static string GetFullQualifiedName(ITypeSymbol type)
    {
        return type.ToDisplayString(FullyQualifiedNoAliasFormat);
    }
}
