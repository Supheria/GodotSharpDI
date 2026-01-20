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

    public static ImmutableArray<INamedTypeSymbol> GetTypeArrayArgument(
        AttributeData attribute,
        string name
    )
    {
        foreach (var arg in attribute.NamedArguments)
        {
            if (arg.Key != name)
            {
                continue;
            }
            var typedConstant = arg.Value;
            if (typedConstant.Kind != TypedConstantKind.Array)
            {
                return ImmutableArray<INamedTypeSymbol>.Empty;
            }
            var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
            foreach (var element in typedConstant.Values)
            {
                if (element.Value is INamedTypeSymbol typeSymbol)
                {
                    builder.Add(typeSymbol);
                }
            }
            return builder.ToImmutable();
        }
        return ImmutableArray<INamedTypeSymbol>.Empty;
    }
}
