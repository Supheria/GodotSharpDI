using Microsoft.CodeAnalysis;

namespace GodotSharpDI.Generator.Internal.Helpers;

internal static class DisplayFormats
{
    private static readonly SymbolDisplayFormat TypeFullQualified = new(
        // 启用 global::
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        // 完全限定名（包含命名空间 + 外部类型 + 当前类型）
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        // 泛型类型参数
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
            | SymbolDisplayGenericsOptions.IncludeVariance,
        // 不使用别名（int → System.Int32）
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
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
