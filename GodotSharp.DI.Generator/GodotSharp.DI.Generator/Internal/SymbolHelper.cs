using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class SymbolHelper
{
    public static bool HasAttribute(ISymbol symbol, INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return false;

        return symbol
            .GetAttributes()
            .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol));
    }

    public static bool ImplementsInterface(INamedTypeSymbol type, INamedTypeSymbol? iface)
    {
        if (iface is null)
            return false;

        return type.AllInterfaces.Any(i => SymbolEqualityComparer.Default.Equals(i, iface));
    }

    public static bool IsNode(INamedTypeSymbol type, CachedSymbol cached)
    {
        var current = type;
        while (current is not null)
        {
            if (SymbolEqualityComparer.Default.Equals(current, cached.GodotNode))
                return true;

            current = current.BaseType;
        }
        return false;
    }

    private static List<INamedTypeSymbol> ParseServiceTypes(AttributeData attr)
    {
        if (attr.ConstructorArguments.Length == 0)
        {
            return [];
        }
        var result = new List<INamedTypeSymbol>();
        var arg = attr.ConstructorArguments[0];
        // 多个参数 → 数组
        if (arg.Kind == TypedConstantKind.Array)
        {
            foreach (var v in arg.Values)
            {
                if (v.Value is INamedTypeSymbol t)
                {
                    result.Add(t);
                }
            }
        }
        // 单个参数 → Copilot 说有可能 Type
        else if (arg.Value is INamedTypeSymbol singleType)
        {
            result.Add(singleType);
        }
        return result;
    }

    public static INamedTypeSymbol[] GetServiceTypesFromAttribute(
        ISymbol symbol,
        INamedTypeSymbol? attributeSymbol
    )
    {
        var attrs = symbol
            .GetAttributes()
            .Where(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol))
            .ToArray();
        if (attrs.Length == 0)
        {
            // 无 Attribute时
            // 类型级：默认自身类型
            // 成员级： 不注入
            return symbol is INamedTypeSymbol t ? [t] : [];
        }
        var attr = attrs[0]; // 禁止重复同一 Attribute ( AllowMultiple = false )
        var result = ParseServiceTypes(attr);
        if (result.Count == 0)
        {
            switch (symbol)
            {
                case INamedTypeSymbol type:
                    result.Add(type);
                    break;
                case IPropertySymbol p:
                    if (p.Type is INamedTypeSymbol t1)
                    {
                        result.Add(t1);
                    }
                    break;
                case IFieldSymbol f:
                    if (f.Type is INamedTypeSymbol t2)
                    {
                        result.Add(t2);
                    }
                    break;
            }
        }
        return result.ToArray();
    }
}
