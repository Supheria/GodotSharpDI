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

    private static List<ITypeSymbol> ParseServiceTypes(AttributeData attr)
    {
        if (attr.ConstructorArguments.Length == 0)
        {
            return [];
        }
        var result = new List<ITypeSymbol>();
        var arg = attr.ConstructorArguments[0];
        // 多个参数 → 数组
        if (arg.Kind == TypedConstantKind.Array)
        {
            foreach (var v in arg.Values)
            {
                if (v.Value is ITypeSymbol t)
                {
                    result.Add(t);
                }
            }
        }
        // 单个参数 → Copilot 说有可能 Type
        else if (arg.Value is ITypeSymbol singleType)
        {
            result.Add(singleType);
        }
        return result;
    }

    public static ITypeSymbol[] GetServiceTypesFromAttribute(
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
                case INamedTypeSymbol t:
                    // 类型级 fallback：自身类型
                    result.Add(t);
                    break;
                case IPropertySymbol p:
                    // 成员级 fallback：成员类型
                    result.Add(p.Type);
                    break;
                case IFieldSymbol f:
                    result.Add(f.Type);
                    break;
            }
        }
        return result.ToArray();
    }
}
