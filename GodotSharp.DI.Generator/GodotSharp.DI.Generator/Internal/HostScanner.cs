using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace GodotSharp.DI.Generator.Internal;

internal static class HostScanner
{
    public static HostDescriptor Analyze(INamedTypeSymbol type, SymbolCache symbolCache)
    {
        var singletonServices = new List<(string, INamedTypeSymbol)>();
        foreach (var member in type.GetMembers())
        {
            if (member is IPropertySymbol or IFieldSymbol)
            {
                var services = SymbolHelper.GetServiceTypesFromAttribute(
                    member,
                    symbolCache.SingletonAttribute
                );
                foreach (var s in services)
                {
                    singletonServices.Add((member.Name, s));
                }
            }
        }
        var isNode = SymbolHelper.IsNode(type, symbolCache);
        return new HostDescriptor(isNode, type, singletonServices);
    }
}
