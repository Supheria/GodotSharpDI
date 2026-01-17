using System.Collections.Immutable;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Data;
using GodotSharp.DI.Generator.Internal.Helpers;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Extensions;

public static class ClassTypeExtensions
{
    // ============================================================
    // Attribute
    // ============================================================
    public static bool HasAttribute(this ClassType type, INamedTypeSymbol? attributeType)
    {
        return AttributeHelper.HasAttribute(type.Symbol, attributeType);
    }

    public static AttributeData? GetAttribute(this ClassType type, INamedTypeSymbol? attributeType)
    {
        return AttributeHelper.GetAttribute(type.Symbol, attributeType);
    }

    public static ImmutableArray<AttributeData> GetAttributes(
        this ClassType type,
        INamedTypeSymbol? attributeType
    )
    {
        return AttributeHelper.GetAttributes(type.Symbol, attributeType);
    }

    public static Location GetAttributeLocation(
        this ClassType type,
        INamedTypeSymbol? attributeType
    )
    {
        var attribute = AttributeHelper.GetAttribute(type.Symbol, attributeType);
        return AttributeHelper.GetAttributeLocation(
            attribute,
            type.DeclarationSyntax.Identifier.GetLocation()
        );
    }

    // ============================================================
    // Interface / Inheritance
    // ============================================================
    public static bool ImplementsInterface(this ClassType type, INamedTypeSymbol? interfaceSymbol)
    {
        if (interfaceSymbol is null)
            return false;

        return type.Symbol.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, interfaceSymbol)
        );
    }

    public static bool Inherits(this ClassType type, INamedTypeSymbol? baseType)
    {
        if (baseType is null)
            return false;

        for (var t = type.Symbol; t != null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t, baseType))
                return true;
        }
        return false;
    }
}
