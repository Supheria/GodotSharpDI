using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class UserScanner
{
    public static UserDescriptor Analyze(INamedTypeSymbol type, SymbolCache symbolCache)
    {
        var dependencies = new List<(string, INamedTypeSymbol)>();
        foreach (var member in type.GetMembers())
        {
            switch (member)
            {
                case IPropertySymbol p:
                {
                    if (
                        SymbolHelper.HasAttribute(p, symbolCache.InjectAttribute)
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
                        SymbolHelper.HasAttribute(f, symbolCache.InjectAttribute)
                        && f.Type is INamedTypeSymbol fieldType
                    )
                    {
                        dependencies.Add((f.Name, fieldType));
                    }
                    break;
                }
            }
        }
        var isNode = SymbolHelper.IsNode(type, symbolCache);
        var isServiceAware = SymbolHelper.ImplementsInterface(
            type,
            symbolCache.ServicesReadyInterface
        );
        return new UserDescriptor(isNode, type, dependencies, isServiceAware);
    }
}
