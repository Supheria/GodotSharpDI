using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class HostServiceCollector
{
    public static HostServiceInfo Analyze(INamedTypeSymbol type, CachedSymbol cachedSymbol)
    {
        var singletonServices = new List<(string, INamedTypeSymbol)>();
        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol or IFieldSymbol)
            {
                var services = SymbolHelper.GetServiceTypesFromAttribute(
                    member,
                    cachedSymbol.SingletonServiceAttribute
                );
                foreach (var s in services)
                {
                    singletonServices.Add((member.Name, s));
                }
            }
        }
        var isNode = SymbolHelper.IsNode(type, cachedSymbol);
        return new HostServiceInfo(isNode, type, singletonServices);
    }
}
