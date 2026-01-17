using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Helpers;

internal static class AttributeHelper
{
    public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeType)
    {
        return GetAttribute(symbol, attributeType) is not null;
    }

    public static AttributeData? GetAttribute(ISymbol symbol, INamedTypeSymbol? attributeType)
    {
        if (attributeType is null)
            return null;

        return symbol
            .GetAttributes()
            .FirstOrDefault(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType)
            );
    }

    public static ImmutableArray<AttributeData> GetAttributes(
        ISymbol symbol,
        INamedTypeSymbol? attributeType
    )
    {
        if (attributeType is null)
            return ImmutableArray<AttributeData>.Empty;

        return symbol
            .GetAttributes()
            .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeType))
            .ToImmutableArray();
    }

    public static Location GetAttributeLocation(AttributeData? attribute, Location fallbackLocation)
    {
        return attribute?.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? fallbackLocation;
    }

    // 泛型版本：读取单个命名参数
    public static bool TryGetNamedArgument<T>(AttributeData attribute, string name, out T? value)
    {
        var arg = attribute.NamedArguments.FirstOrDefault(kvp => kvp.Key == name);

        if (!arg.Value.IsNull && arg.Value.Value is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }

    // 读取类型数组参数（常用于 Modules.Instantiate）
    public static ImmutableArray<INamedTypeSymbol> GetTypeArrayArgument(
        AttributeData attribute,
        string name
    )
    {
        if (!TryGetNamedArgument<ImmutableArray<TypedConstant>>(attribute, name, out var array))
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        return array
            .Where(tc => tc.Value is INamedTypeSymbol)
            .Select(tc => (INamedTypeSymbol)tc.Value!)
            .ToImmutableArray();
    }
}
