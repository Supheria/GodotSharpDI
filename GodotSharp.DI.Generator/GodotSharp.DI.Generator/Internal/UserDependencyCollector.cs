using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class UserDependencyCollector
{
    public static UserDependencyInfo Analyze(INamedTypeSymbol type, CachedSymbol cachedSymbol)
    {
        var dependencies = new List<(string, INamedTypeSymbol)>();
        foreach (var member in type.GetMembers())
        {
            switch (member)
            {
                case IPropertySymbol p:
                {
                    if (
                        SymbolHelper.HasAttribute(p, cachedSymbol.DependencyAttribute)
                        && p.Type is INamedTypeSymbol propertyType
                    )
                    {
                        dependencies.Add((p.Name, propertyType));
                    }
                    break;
                }
                case IFieldSymbol f:
                {
                    if (
                        SymbolHelper.HasAttribute(f, cachedSymbol.DependencyAttribute)
                        && f.Type is INamedTypeSymbol fieldType
                    )
                    {
                        dependencies.Add((f.Name, fieldType));
                    }
                    break;
                }
            }
        }
        var isNode = SymbolHelper.IsNode(type, cachedSymbol);
        var isServiceAware = SymbolHelper.ImplementsInterface(
            type,
            cachedSymbol.ServiceAwareInterface
        );
        return new UserDependencyInfo(isNode, type, dependencies, isServiceAware);
    }
}
