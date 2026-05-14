using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharpDI.SourceGenerator.Internal.Helpers;

/// <summary>
/// Symbol extension methods
/// </summary>
internal static class SymbolExtensions
{
    /// <summary>
    /// Get the fully qualified name of a type (with global:: and namespace)
    /// </summary>
    public static string ToFullyQualifiedName(this ITypeSymbol type)
    {
        return DisplayFormats.GetFullQualifiedName(type);
    }

    /// <summary>
    /// Check if a type implements a specified interface
    /// </summary>
    public static bool ImplementsInterface(this ITypeSymbol type, INamedTypeSymbol interfaceType)
    {
        return type.AllInterfaces.Contains(interfaceType, SymbolEqualityComparer.Default);
    }

    /// <summary>
    /// Check if a type inherits from a specified base class
    /// </summary>
    public static bool InheritsFrom(this ITypeSymbol type, INamedTypeSymbol baseType)
    {
        var current = type.BaseType;
        while (current != null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
            current = current.BaseType;
        }
        return false;
    }

    /// <summary>
    /// Check if a symbol has a specified attribute
    /// </summary>
    public static bool HasAttribute(this ISymbol symbol, INamedTypeSymbol? attributeType)
    {
        if (attributeType == null)
            return false;

        return symbol
            .GetAttributes()
            .Any(attr => SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType));
    }

    /// <summary>
    /// Get a specified attribute from a symbol
    /// </summary>
    public static AttributeData? GetAttribute(this ISymbol symbol, INamedTypeSymbol? attributeType)
    {
        if (attributeType == null)
            return null;

        return symbol
            .GetAttributes()
            .FirstOrDefault(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType)
            );
    }

    /// <summary>
    /// Get all specified attributes from a symbol
    /// </summary>
    public static IEnumerable<AttributeData> GetAttributes(
        this ISymbol symbol,
        INamedTypeSymbol? attributeType
    )
    {
        if (attributeType == null)
            return Enumerable.Empty<AttributeData>();

        return symbol
            .GetAttributes()
            .Where(attr =>
                SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attributeType)
            );
    }

    /// <summary>
    /// Check if a type is an interface or concrete class
    /// </summary>
    public static bool IsInterfaceOrConcreteClass(this ITypeSymbol type)
    {
        // Check that it cannot be abstract class or static class
        if (type.IsAbstract && type.TypeKind == TypeKind.Class)
            return false;
        if (type.IsStatic)
            return false;

        // Must be interface or class
        return type.TypeKind == TypeKind.Interface || type.TypeKind == TypeKind.Class;
    }

    /// <summary>
    /// Check if a type is a concrete class
    /// </summary>
    public static bool IsConcreteClass(this ITypeSymbol type)
    {
        // Check that it cannot be abstract class or static class
        if (type.IsAbstract || type.IsStatic)
            return false;

        // Must be class
        return type.TypeKind == TypeKind.Class;
    }

    /// <summary>
    /// Check if a type is a valid exposed type
    /// </summary>
    public static bool IsValidExposedType(this ITypeSymbol type)
    {
        // Must be interface
        return type.TypeKind == TypeKind.Interface;
    }

    /// <summary>
    /// Get all members (fields and properties) of a type
    /// </summary>
    public static IEnumerable<ISymbol> GetFieldsAndProperties(this INamedTypeSymbol type)
    {
        return type.GetMembers()
            .Where(m => m.Kind == SymbolKind.Field || m.Kind == SymbolKind.Property);
    }

    /// <summary>
    /// Get all non-static constructors of a type
    /// </summary>
    public static IEnumerable<IMethodSymbol> GetInstanceConstructors(this INamedTypeSymbol type)
    {
        return type.Constructors.Where(c => !c.IsStatic);
    }

    /// <summary>
    /// Check if a symbol is public
    /// </summary>
    public static bool IsPublic(this ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Public;
    }

    /// <summary>
    /// Check if a symbol is private
    /// </summary>
    public static bool IsPrivate(this ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Private;
    }

    /// <summary>
    /// Check if a symbol is protected
    /// </summary>
    public static bool IsProtected(this ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Protected;
    }

    /// <summary>
    /// Check if a symbol is internal
    /// </summary>
    public static bool IsInternal(this ISymbol symbol)
    {
        return symbol.DeclaredAccessibility == Accessibility.Internal;
    }

    /// <summary>
    /// Check if a type is an unbound generic type
    /// Unbound generic: Contains unbound type parameters, e.g., List&lt;T&gt;
    /// Closed generic: All type parameters are bound, e.g., List&lt;int&gt;
    /// </summary>
    public static bool IsUnboundGenericType(this INamedTypeSymbol type)
    {
        if (!type.IsGenericType)
            return false;

        // Check if there are unbound type parameters in the type arguments
        return type.TypeArguments.Any(arg => arg.Kind == SymbolKind.TypeParameter);
    }
}
