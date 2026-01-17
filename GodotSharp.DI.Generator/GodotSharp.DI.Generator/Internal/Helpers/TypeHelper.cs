using System;
using System.Collections.Generic;
using System.Linq;
using GodotSharp.DI.Generator.Internal.Descriptors;
using GodotSharp.DI.Shared;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal.Helpers;

internal static class TypeHelper
{
    // ============================================================
    // Interface / Inheritance
    // ============================================================
    public static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol? interfaceSymbol)
    {
        if (interfaceSymbol is null)
            return false;

        return type.AllInterfaces.Any(i =>
            SymbolEqualityComparer.Default.Equals(i, interfaceSymbol)
        );
    }

    public static bool Inherits(INamedTypeSymbol type, INamedTypeSymbol? baseType)
    {
        if (baseType is null)
            return false;

        for (var t = type; t != null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t, baseType))
                return true;
        }
        return false;
    }

    // ============================================================
    // DI relevance
    // ============================================================

    private static bool IsDiNamespace(INamedTypeSymbol symbol)
    {
        var ns = symbol.ContainingNamespace?.ToString();
        return ns == NamespaceNames.Abstractions;
    }

    private static bool IsDiAttribute(INamedTypeSymbol? attr)
    {
        if (attr is null || !IsDiNamespace(attr))
            return false;

        return attr.MetadataName switch
        {
            TypeNames.ModulesAttribute => true,
            TypeNames.InjectAttribute => true,
            TypeNames.SingletonAttribute => true,
            TypeNames.TransientAttribute => true,
            TypeNames.HostAttribute => true,
            TypeNames.UserAttribute => true,
            _ => false,
        };
    }

    private static bool IsDiInterface(INamedTypeSymbol iface)
    {
        if (!IsDiNamespace(iface))
            return false;

        return iface.MetadataName switch
        {
            TypeNames.ScopeInterface => true,
            TypeNames.ServicesReadyInterface => true,
            _ => false,
        };
    }

    public static bool IsDiRelevant(INamedTypeSymbol type)
    {
        if (type.TypeKind != TypeKind.Class)
            return false;

        foreach (var attr in type.GetAttributes())
        {
            if (IsDiAttribute(attr.AttributeClass))
                return true;
        }

        foreach (var iface in type.AllInterfaces)
        {
            if (IsDiInterface(iface))
                return true;
        }

        return false;
    }
}
