using System.Collections.Immutable;
using System.Linq;
using GodotSharpDI.SourceGenerator.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

/// <summary>
/// Attribute helper class - Used for attribute-related operations
/// </summary>
internal static class AttributeHelper
{
    /// <summary>
    /// Get exposed service types from Provide member
    /// </summary>
    public static ImmutableArray<INamedTypeSymbol> GetMemberExposedTypes(
        ISymbol member,
        CachedSymbols symbols
    )
    {
        var provideAttr = member.GetAttribute(symbols.ProvideAttribute);
        var exposedTypes = GetTypesFromAttribute(provideAttr, ShortNames.ExposedTypes);

        // If no service type is specified, use the member's type
        if (exposedTypes.IsEmpty)
        {
            ITypeSymbol? memberType = null;
            if (member is IFieldSymbol field)
            {
                memberType = field.Type;
            }
            else if (member is IPropertySymbol property)
            {
                memberType = property.Type;
            }
            else if (member is IMethodSymbol method)
            {
                memberType = method.ReturnType;
            }
            if (memberType is INamedTypeSymbol namedType)
            {
                // For async members (Task<T> / ValueTask<T>) without ExposedTypes specified,
                // service type should be the inner T, not Task<T> itself
                if (
                    symbols.IsAsyncType(namedType)
                    && namedType.IsGenericType
                    && namedType.TypeArguments[0] is INamedTypeSymbol innerType
                )
                {
                    return ImmutableArray.Create(innerType);
                }
                return ImmutableArray.Create(namedType);
            }
        }

        return exposedTypes;
    }

    /// <summary>
    /// Extract type array from attribute parameters
    /// </summary>
    public static ImmutableArray<INamedTypeSymbol> GetTypesFromAttribute(
        AttributeData? attr,
        string propertyName
    )
    {
        if (attr == null)
        {
            return ImmutableArray<INamedTypeSymbol>.Empty;
        }

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        // Constructor arguments
        if (attr.ConstructorArguments.Length > 0)
        {
            foreach (var arg in attr.ConstructorArguments)
            {
                if (arg.Kind == TypedConstantKind.Array)
                {
                    foreach (var item in arg.Values)
                    {
                        if (item.Value is INamedTypeSymbol type)
                            builder.Add(type);
                    }
                }
            }
        }

        // Named arguments
        foreach (var namedArg in attr.NamedArguments)
        {
            if (namedArg.Key == propertyName && namedArg.Value.Kind == TypedConstantKind.Array)
            {
                foreach (var item in namedArg.Value.Values)
                {
                    if (item.Value is INamedTypeSymbol type)
                        builder.Add(type);
                }
            }
        }

        return builder.ToImmutable();
    }
}
