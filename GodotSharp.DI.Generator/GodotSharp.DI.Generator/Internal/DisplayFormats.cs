using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

public static class DisplayFormats
{
    public static readonly SymbolDisplayFormat TypeFormat = new SymbolDisplayFormat(
        // 1. 启用 global::
        globalNamespaceStyle: SymbolDisplayGlobalNamespaceStyle.Included,
        // 2. 完全限定名（包含命名空间 + 外部类型 + 当前类型）
        typeQualificationStyle: SymbolDisplayTypeQualificationStyle.NameAndContainingTypesAndNamespaces,
        // 3. 泛型类型参数
        genericsOptions: SymbolDisplayGenericsOptions.IncludeTypeParameters
            | SymbolDisplayGenericsOptions.IncludeVariance,
        // 4. 不使用别名（int → System.Int32）
        miscellaneousOptions: SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier
    );
}
