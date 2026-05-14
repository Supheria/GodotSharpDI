using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

internal static class DisplayFormats
{
    private static readonly SymbolDisplayFormat TypeFullQualified = new(
        // Enable global::
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        // Fully qualified name (including namespace + outer type + current type)
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        // Generic type parameters
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
            | SymbolDisplayGenericsOptions.IncludeVariance,
        // Don't use aliases (int → System.Int32)
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    private static readonly SymbolDisplayFormat ClassName = new(
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Omitted,
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameOnly,
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
            | SymbolDisplayGenericsOptions.IncludeVariance,
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
    );

    public static bool GetNamespace(INamedTypeSymbol type, out string namespaceName)
    {
        if (type.ContainingNamespace.IsGlobalNamespace)
        {
            namespaceName = string.Empty;
            return false;
        }
        namespaceName = type.ContainingNamespace.ToDisplayString();
        return true;
    }

    public static string GetClassName(INamedTypeSymbol type)
    {
        return type.ToDisplayString(ClassName);
    }

    public static string GetFullQualifiedName(ITypeSymbol type)
    {
        return type.ToDisplayString(TypeFullQualified);
    }
}
